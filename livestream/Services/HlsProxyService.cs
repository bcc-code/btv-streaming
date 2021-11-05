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
    public class HlsProxyService
    {
        private readonly ILogger<HlsProxyService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public HlsProxyService(ILogger<HlsProxyService> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<string> RetrieveAndModifyTopLevelManifestForToken(string topLevelManifestUrl, string token, string proxySecondLevelBaseUrl)
        {
            var topLevelManifestContent = await GetRawContentAsync(topLevelManifestUrl);
            var topLevelManifestBaseUrl = topLevelManifestUrl.Substring(0, topLevelManifestUrl.IndexOf("/index", System.StringComparison.OrdinalIgnoreCase));

            var urlEncodedTopLevelManifestBaseUrl = HttpUtility.UrlEncode(topLevelManifestBaseUrl);
            token = new string(token.Where(c => char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-').ToArray());
            var urlEncodedToken = HttpUtility.UrlEncode(token);

            string generateSecondLevelProxyUrl(string path)
            {
                return $"{proxySecondLevelBaseUrl}?url={urlEncodedTopLevelManifestBaseUrl}{HttpUtility.UrlEncode("/" + path)}&token={urlEncodedToken}";
            }

            var newContent = Regex.Replace(topLevelManifestContent, @"(index_\d+\.m3u8)", (Match m) => generateSecondLevelProxyUrl(m.Value));
            newContent = Regex.Replace(newContent, @"#EXT-X-MEDIA:TYPE=SUBTITLES.+(?=index_.+\.m3u8)", (Match m) => $"{m.Value}{topLevelManifestBaseUrl}/{m.Groups[1].Value}");
            newContent = Regex.Replace(newContent, @"(#EXT-X-MEDIA:TYPE=AUDIO.+)(index_.+\.m3u8)", (Match m) => $"{m.Groups[1].Value}{generateSecondLevelProxyUrl(m.Groups[2].Value)}");
            newContent = SortAudioTracks(newContent);
            newContent = newContent.Replace("#EXT-X-VERSION:3", "#EXT-X-VERSION:7");
            newContent = newContent.Replace("#EXT-X-TARGETDURATION:12", "#EXT-X-TARGETDURATION:6");


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

        public async Task<string> RetrieveAndModifySecondLevelManifestAsync(string url, Func<string, string, string> generateKeyDeliveryUrl)
        {
            const string playlistRegex = @"index_[^\s]+?(?:ts|aac|mp4)";
            const string urlRegex = @"(?:URI=).https?:\/\/[\da-z\.]+\.[a-z\.]{2,6}[\/\w \.-](.+)\/(.+).";
            // The regex captures URI="https://blab123labla.anything.anything.anything/{1}/{2}"

            var baseUrl = url.Substring(0, url.IndexOf("/index", StringComparison.OrdinalIgnoreCase));
            var content = await GetRawContentAsync(url);

            var newContent = Regex.Replace(content, urlRegex, m => $"URI=\"{generateKeyDeliveryUrl(m.Groups[1].Value, m.Groups[2].Value)}\"");
            newContent = Regex.Replace(newContent, playlistRegex, m => string.Format(CultureInfo.InvariantCulture, baseUrl + "/" + m.Value));
            newContent = newContent.Replace("#EXT-X-VERSION:3", "#EXT-X-VERSION:7");
            newContent = newContent.Replace("#EXT-X-TARGETDURATION:12", "#EXT-X-TARGETDURATION:6");
            return newContent;
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
