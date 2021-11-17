using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using System.Globalization;

    namespace LivestreamFunctions.Services
{
    public class CmafProxyService
    {
        private readonly ILogger<HlsProxyService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public CmafProxyService(ILogger<HlsProxyService> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<string> RetrieveAndModifyTopLevelManifestForToken(string topLevelManifestUrl, string proxySecondLevelBaseUrl)
        {
            var topLevelManifestContent = await GetRawContentAsync(topLevelManifestUrl);
            var topLevelManifestBaseUrl = topLevelManifestUrl.Substring(0, topLevelManifestUrl.IndexOf("/index", System.StringComparison.OrdinalIgnoreCase));
            var queryParams = topLevelManifestUrl[topLevelManifestUrl.IndexOf("?")..];


            var urlEncodedTopLevelManifestBaseUrl = HttpUtility.UrlEncode(topLevelManifestBaseUrl);

            string generateSecondLevelProxyUrl(string path)
            {
                return $"{proxySecondLevelBaseUrl}?url={urlEncodedTopLevelManifestBaseUrl}{HttpUtility.UrlEncode("/" + path)}{HttpUtility.UrlEncode(queryParams)}";
            }

            var newContent = Regex.Replace(topLevelManifestContent, @"(index_\d+\.m3u8)", (Match m) => generateSecondLevelProxyUrl(m.Value));
            newContent = Regex.Replace(newContent, @"(#EXT-X-MEDIA:TYPE=SUBTITLES.+)(index_.+\.m3u8)", (Match m) => $"{m.Groups[1].Value}{generateSecondLevelProxyUrl(m.Groups[2].Value)}");
            newContent = Regex.Replace(newContent, @"(#EXT-X-MEDIA:TYPE=AUDIO.+)(index_.+\.m3u8)", (Match m) => $"{m.Groups[1].Value}{generateSecondLevelProxyUrl(m.Groups[2].Value)}");
            newContent = SortAudioTracks(newContent);


            return newContent;
        }

        public string SortAudioTracks(string manifest)
        {
            var lines = manifest.Split('\n').ToList();

            var tolkLine = lines.FirstOrDefault(line => line.Contains("LANGUAGE=\"no-x-tolk\"", StringComparison.InvariantCultureIgnoreCase));
            if (string.IsNullOrEmpty(tolkLine)) return manifest;

            lines.Remove(tolkLine);

            var norwegianLineIndex = lines.FindIndex(l => l.Contains("LANGUAGE=\"nor\"", StringComparison.InvariantCultureIgnoreCase));
            if (norwegianLineIndex == -1) return manifest;

            lines.Insert(norwegianLineIndex+1, tolkLine);

            return string.Join('\n', lines);
        }

        public string ModifyManifestToBeAudioOnly(string topLevelManifest, string language)
        {
            string codec = null;
            foreach (Match match in Regex.Matches(topLevelManifest, "#EXT-X-STREAM-INF:.+CODECS=\".+?,(?<audiocodec>.+?)\""))
            {
                if (!match.Success || match.Groups.Count == 0) continue;
                codec = match.Groups["audiocodec"].Value;
                break;
            }

            var newManifest = "#EXTM3U" + "\n";
            newManifest += "#EXT-X-VERSION:7" + "\n";
            newManifest += "#EXT-X-INDEPENDENT-SEGMENTS" + "\n";

            Regex regex = new Regex("#EXT-X-MEDIA:TYPE=AUDIO.+URI=\"(?<uri>.+)\"");
            if (!string.IsNullOrWhiteSpace(language))
            {
                language = new string(language.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
                regex = new Regex($"#EXT-X-MEDIA:TYPE=AUDIO.+LANGUAGE=\"{language.ToLower()}\".+URI=\"(?<uri>.+)\"");
            }

            var matches = regex.Matches(topLevelManifest);
            foreach (Match match in matches)
            {
                if (!match.Success) continue;
                newManifest += $"#EXT-X-STREAM-INF:BANDWIDTH=128000,CODECS=\"{codec}\"" + "\n";
                newManifest += match.Groups["uri"] + "\n";
            }

            return newManifest;
        }

        public async Task<string> RetrieveAndModifySecondLevelManifestAsync(string url)
        {
            const string playlistRegex = @"\.\.\/[^\s]+?(?:ts|aac|mp4|vtt)(.+)?";
            const string urlRegex = @"(?:URI=).https?:\/\/[\da-z\.]+\.[a-z\.]{2,6}[\/\w \.-](.+?)\/(.+?)""";
            // The regex captures URI="https://blab123labla.anything.anything.anything/{1}/{2}"

            var content = await GetRawContentAsync(url);

            content = ConvertRelativeUrlsToAbsolute(content, url);
            return content;
        }


        private static string GetAbsoluteBaseUrl(string url)
        {
            return url.Substring(0, url.LastIndexOf("/"));
        }

        private static string ConvertRelativeUrlsToAbsolute(string manifest, string manifestUrl)
        {
            var queryParams = manifestUrl[manifestUrl.IndexOf("?")..];
            var baseUrl = GetAbsoluteBaseUrl(manifestUrl);
            // https://datatracker.ietf.org/doc/html/draft-pantos-hls-rfc8216bis#section-4.1
            // Lines not starting with # is a file/playlist url.
            // If relative, make it absolute.
            manifest = Regex.Replace(manifest, @"^(?!https?:\/\/)[^#\s].+", baseUrl + "/$&" + queryParams, RegexOptions.Multiline);
            manifest = Regex.Replace(manifest, @"URI=""(?!https?:\/\/)(.+?)""", $"URI=\"{baseUrl}/$1{queryParams}\"");
            return manifest;
        }


        private async Task<string> GetRawContentAsync(string uri)
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, uri);

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            var response = await client.SendAsync(httpRequest);
            return await response.Content.ReadAsStringAsync();
        }
    }
}
