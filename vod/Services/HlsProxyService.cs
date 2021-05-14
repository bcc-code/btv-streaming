using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using VODFunctions.Model;

namespace VODFunctions.Services
{
    public class HlsProxyService
    {
        private readonly ILogger<HlsProxyService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly SubtitleService _subtitleService;
        private readonly VODOptions _vodOptions;

        public HlsProxyService(ILogger<HlsProxyService> logger,
            IHttpClientFactory httpClientFactory,
            SubtitleService subtitleService,
            IOptions<VODOptions> vodOptions)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _subtitleService = subtitleService;
            _vodOptions = vodOptions.Value;
        }

        public async Task<string> ProxyTopLevelAsync(
            string manifestUrl,
            string token,
            bool subs,
            bool max720p,
            Func<(string a, string b), string> getSecondLevelProxyUrl)
        {
            /*** 
            * fetch
            * customize
            * proxy second-level manifests: QualityLevels(\d+)/Manifest(.+)
            ***/

            var manifest = await GetManifestAsync(manifestUrl);
            manifest = await CustomizeTopLevelManifestAsync(manifest, manifestUrl, subs, max720p);
            manifest = AddTokenToTopLevelManifest(manifest, manifestUrl, token, getSecondLevelProxyUrl);
            return manifest;
        }

        public async Task<string> CustomizeTopLevelManifestAsync(
            string manifest,
            string manifestUrl,
            bool subs,
            bool max720p,
            Func<(string a, string b), string> getSecondLevelProxyUrl)
        {
            var videoFilename = Regex.Match(manifestUrl, @"[^\/]*?(?=\.ism\/)")?.Value;
            manifest = ConvertRelativeUrlsToAbsolute(manifest, manifestUrl);
            manifest = SetAudioDefaults(manifest);

            if (max720p)
            {
                manifest = RemoveQualityLevel(manifest, "1920x1080");
            }

            if (subs && !string.IsNullOrWhiteSpace(videoFilename))
            {
                manifest = await AddSubtitlesAsync(manifest, videoFilename);
            }

            return manifest;
        }

        private async Task<string> AddSubtitlesAsync(string manifest, string videoFilename)
        {
            var subs = await _subtitleService.GetByVideoFileName(videoFilename);
            var proxyUrl = "https://proxy.brunstad.tv/api/vod/subtitles?subs=";
            var hasSubs = false;

            foreach (var sub in subs.Where(s => s.Type == "vtt"))
            {
                hasSubs = true;
                manifest += $"#EXT-X-MEDIA:TYPE=SUBTITLES,NAME=\"{sub.Label}\",DEFAULT=NO,AUTOSELECT=NO,FORCED=NO,LANGUAGE=\"{sub.LanguageCode}\",GROUP-ID=\"subs\",URI=\"{proxyUrl + HttpUtility.UrlEncode(sub.Url)}\"\n";
            }

            if (hasSubs)
            {
                manifest = manifest.Replace("AUDIO=\"audio\"", "AUDIO=\"audio\",SUBTITLES=\"subs\"");
            }

            return manifest;
        }

        private string ProxySecondLevelManifests(string manifestUrl)
        {
            var nameMatch = Regex.Match(manifestUrl, @"\/.*\/(.*)\.ism");
            var name = nameMatch?.Value;
            return name;
        }

        private static string GetVideoFilename(string manifestUrl)
        {
            var nameMatch = Regex.Match(manifestUrl, @"\/.*\/(.*)\.ism");
            var name = nameMatch?.Value;
            return name;
        }

        private static string RemoveQualityLevel(string manifest, string resolution)
        {
            manifest = Regex.Replace(manifest, $"#EXT-X-STREAM-INF:(?=.+RESOLUTION={resolution}).+\n.+\n", (Match m) => "");
            manifest = Regex.Replace(manifest, $"#EXT-X-I-FRAME-STREAM-INF:(?=.+RESOLUTION={resolution}).+\n", (Match m) => "");
            return manifest;
        }

        private static string SetAudioDefaults(string manifest)
        {
            manifest = Regex.Replace(manifest, @"(#EXT-X-MEDIA:TYPE=AUDIO.+)(DEFAULT=..S?)", (Match m) => $"{m.Groups[1].Value}DEFAULT=NO");
            manifest = Regex.Replace(manifest, @"(#EXT-X-MEDIA:TYPE=AUDIO.+)(AUTOSELECT=..S?)", (Match m) => $"{m.Groups[1].Value}AUTOSELECT=YES");
            manifest = Regex.Replace(manifest, "(#EXT-X-MEDIA:TYPE=AUDIO(?=.*?LANGUAGE=\"nor\").*?)(DEFAULT=..S?)", (Match m) => $"{m.Groups[1].Value}DEFAULT=YES");
            manifest = Regex.Replace(manifest, "(#EXT-X-MEDIA:TYPE=AUDIO(?=.*?LANGUAGE=\"nor\").*?)(AUTOSELECT=..S?)", (Match m) => $"{m.Groups[1].Value}AUTOSELECT=YES");
            return manifest;
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

        public string ModifySecondLevelManifest(string manifest, string manifestUrl, string token)
        {
            // Keydelivery url, add token
            manifest = Regex.Replace(manifest, @"^#EXT-X-KEY:METHOD=AES-128,URI="".+?(?="")", m => m.Value + "?token=" + HttpUtility.UrlEncode(token));
            manifest = ConvertRelativeUrlsToAbsolute(manifest, manifestUrl);
            return manifest;
        }

        private static string ConvertRelativeUrlsToAbsolute(string manifest, string manifestUrl)
        {
            var baseUrl = GetAbsoluteBaseUrl(manifestUrl);
            // https://datatracker.ietf.org/doc/html/draft-pantos-hls-rfc8216bis#section-4.1
            // Lines not starting with # is a file/playlist url.
            // If relative, make it absolute.
            manifest = Regex.Replace(manifest, @"^(?!https?:\/\/)[^#\s].+", baseUrl + "$&");
            manifest = Regex.Replace(manifest, @"URI=""(.+?)""", baseUrl + "$1");
            return manifest;
        }

        private static string GetAbsoluteBaseUrl(string url)
        {
            return url.Substring(0, url.LastIndexOf("/"));
        }

        public async Task<string> GetManifestAsync(string uri)
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, uri);

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            var response = await client.SendAsync(httpRequest);
            return await response.Content.ReadAsStringAsync();
        }
    }
}
