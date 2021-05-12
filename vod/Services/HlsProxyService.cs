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

namespace VODFunctions.Services
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

            string newContent = Regex.Replace(topLevelManifestContent, @"(index_\d+\.m3u8)", (Match m) => generateSecondLevelProxyUrl(m.Value));
            newContent = Regex.Replace(newContent, @"#EXT-X-MEDIA:TYPE=SUBTITLES.+(?=index_.+\.m3u8)", (Match m) => $"{m.Value}{topLevelManifestBaseUrl}/{m.Groups[1].Value}");
            newContent = Regex.Replace(newContent, @"(#EXT-X-MEDIA:TYPE=AUDIO.+)(index_.+\.m3u8)", (Match m) => $"{m.Groups[1].Value}{generateSecondLevelProxyUrl(m.Groups[2].Value)}");

            return newContent;
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
            newManifest += "#EXT-X-VERSION:4" + "\n";
            newManifest += "#EXT-X-INDEPENDENT-SEGMENTS" + "\n";

            Regex regex = new Regex("#EXT-X-MEDIA:TYPE=AUDIO.+URI=\"(?<uri>.+)\"");
            if (!string.IsNullOrWhiteSpace(language))
            {
                language = new string(language.Where(char.IsLetterOrDigit).ToArray());
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

        public async Task<string> RetrieveAndModifySecondLevelManifestAsync(string url, string token)
        {
            var manifest = await GetRawContentAsync(url);

            // Keydelivery url, add token
            manifest = Regex.Replace(manifest, @"^#EXT-X-KEY:METHOD=AES-128,URI="".+?(?="")", m => m.Value + "?token={token}");

            // https://datatracker.ietf.org/doc/html/draft-pantos-hls-rfc8216bis#section-4.1
            // Lines not starting with # is a file/playlist url.
            // If relative, make it absolute.
            manifest = Regex.Replace(manifest, @"^(?!https?:\/\/)[^#\s].+", m => GetAbsoluteBaseUrl(url) + "/" + m.Value);
            return manifest;
        }

        private static string GetAbsoluteBaseUrl(string url)
        {
            return url.Substring(0, url.IndexOf(".ism", StringComparison.OrdinalIgnoreCase)) + ".ism";
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
