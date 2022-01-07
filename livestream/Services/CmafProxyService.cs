using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Net.Http;

namespace LivestreamFunctions.Services
{
    public class CmafProxyService
    {
        private readonly ILogger<CmafProxyService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public CmafProxyService(ILogger<CmafProxyService> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<string> RetrieveAndModifyTopLevelManifest(string topLevelManifestUrl, string proxySecondLevelBaseUrl)
        {
            var manifest = await GetRawContentAsync(topLevelManifestUrl);
            return CustomizeTopLevelManifest(manifest, topLevelManifestUrl, proxySecondLevelBaseUrl);
        }

        public string CustomizeTopLevelManifest(string manifest, string topLevelManifestUrl, string proxySecondLevelBaseUrl)
        {
            var urlBasePath = Regex.Match(topLevelManifestUrl, @"\/[^\/]+\.m3u8");
            if (!urlBasePath.Success)
            {
                return "";
            }

            var topLevelManifestBaseUrl = topLevelManifestUrl.Substring(0, urlBasePath.Index);
            string queryParams = null;
            if (topLevelManifestUrl.IndexOf("?") != -1)
            {
                queryParams = topLevelManifestUrl[(topLevelManifestUrl.IndexOf("?")+1)..];
            }

            string generateSecondLevelProxyUrl(string path)
            {
                var combinedUrl = CombineUrl(topLevelManifestBaseUrl, "/" + path, queryParams);
                return $"{proxySecondLevelBaseUrl}?url={HttpUtility.UrlEncode(combinedUrl)}";
            }

            manifest = Regex.Replace(manifest, @"^(?!https?:\/\/)[^#\s].+", (Match m) => generateSecondLevelProxyUrl(m.Value), RegexOptions.Multiline);
            manifest = Regex.Replace(manifest, @"URI=""(?!https?:\/\/)(.+?)""", m => $"URI=\"{generateSecondLevelProxyUrl(m.Groups[1].Value)}\"");
            manifest = SortAudioTracks(manifest);
            manifest = SortVideoTracks(manifest);

            return manifest;
        }

        public string SortAudioTracks(string manifest)
        {
            var lines = manifest.Split('\n').ToList();

            var tolkLine = lines.FirstOrDefault(line => line.Contains("LANGUAGE=\"no-x-tolk\"", StringComparison.InvariantCultureIgnoreCase));
            if (string.IsNullOrEmpty(tolkLine)) return manifest;

            var norwegianLineIndex = lines.FindIndex(l => l.Contains("LANGUAGE=\"nor\"", StringComparison.InvariantCultureIgnoreCase));
            if (norwegianLineIndex == -1) return manifest;
            
            try
            {
                lines.Remove(tolkLine);
                lines.Insert(norwegianLineIndex+1, tolkLine);
            }
            catch (Exception)
            {
                return manifest;
            }

            return string.Join('\n', lines);
        }

        public string SortVideoTracks(string manifest)
        {
            var lines = manifest.Split('\n').ToList();

            var line540Index = lines.FindIndex(line => line.Contains("RESOLUTION=960x540", StringComparison.InvariantCultureIgnoreCase));
            if (line540Index == -1) return manifest;

            var topLineIndex = lines.FindIndex(l => l.Contains("#EXT-X-INDEPENDENT-SEGMENTS", StringComparison.InvariantCultureIgnoreCase));
            if (topLineIndex == -1) return manifest;

            var line540text = lines[line540Index];
            var line540url = lines[line540Index+1];

            try
            {
                lines.RemoveAt(line540Index);
                lines.RemoveAt(line540Index); // twice to remove url as well

                lines.Insert(topLineIndex + 1, line540url);
                lines.Insert(topLineIndex + 1, line540text);
            } catch (Exception)
            {
                return manifest;
            }

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

            var version = topLevelManifest[topLevelManifest.IndexOf("#EXT-X-VERSION:") + "#EXT-X-VERSION:".Length];
            if (version == default || char.IsWhiteSpace(version))
            {
                version = '6';
            }

            var newManifest = "#EXTM3U" + "\n";
            newManifest += "#EXT-X-VERSION:" + version + "\n";
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
            var content = await GetRawContentAsync(url);
            return CustomizeSecondLevelManifestAsync(content, url);
        }

        public string CustomizeSecondLevelManifestAsync(string content, string url)
        {
            content = ConvertRelativeUrlsToAbsolute(content, url);
            return content;
        }


        private static string GetBaseUrl(string url)
        {
            return url.Substring(0, url.LastIndexOf("/"));
        }

        private static string ConvertRelativeUrlsToAbsolute(string manifest, string manifestUrl)
        {
            var baseUrl = GetBaseUrl(manifestUrl);
            string queryParams = null;
            if (manifestUrl.IndexOf("?") != -1)
            {
                queryParams = manifestUrl[(manifestUrl.IndexOf("?") + 1)..];
            }
            // https://datatracker.ietf.org/doc/html/draft-pantos-hls-rfc8216bis#section-4.1
            // Lines not starting with # is a file/playlist url.
            // If relative, make it absolute.
            manifest = Regex.Replace(
                manifest,
                @"^(?!https?:\/\/)[^#\s].+",
                m => {
                    return CombineUrl(baseUrl, "/" + m.Value, queryParams);
                },
                RegexOptions.Multiline
            );

            manifest = Regex.Replace(
                manifest,
                @"URI=""(?!https?:\/\/)(.+?)""",
                m => {
                    var newUrl = CombineUrl(baseUrl, "/" + m.Groups[1], queryParams);
                    return $"URI=\"{newUrl}\"";
                }
            );

            return manifest;
        }

        /** Path needs leading slash **/
        public static string CombineUrl(string baseUrl, string path, string queryParams = null)
        {
            var absoluteUrl = baseUrl + path;
            if (!string.IsNullOrWhiteSpace(queryParams))
            {
                var querySeperator = path.Contains("?") ? "&" : "?";
                absoluteUrl += querySeperator + queryParams;
            }
            return absoluteUrl;
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
