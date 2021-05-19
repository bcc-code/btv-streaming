using LazyCache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using VODStreaming.Model;

namespace VODStreaming.Services
{
    public class HlsProxyService
    {
        private readonly ILogger<HlsProxyService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly SubtitleService _subtitleService;
        private readonly VODOptions _vodOptions;
        private readonly IAppCache _appCache;

        public HlsProxyService(ILogger<HlsProxyService> logger,
            IHttpClientFactory httpClientFactory,
            SubtitleService subtitleService,
            IOptions<VODOptions> vodOptions,
            IAppCache appCache)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _subtitleService = subtitleService;
            _appCache = appCache;
            _vodOptions = vodOptions.Value;
        }

        public async Task<string> ProxyTopLevelAsync(
            Func<(string url, string token), string> getSecondLevelProxyUrl,
            Func<string, string> getSubtitleProxyUrl,
            string manifestUrl,
            string token,
            bool subs,
            bool max720p,
            bool audioOnly,
            string language)
        {
            if (audioOnly)
            {
                subs = false;
                max720p = false;
            }

            var manifest = await _appCache.GetOrAddAsync(
                $"toplevelmanifest-customized_{manifestUrl.ToLowerInvariant()}_{subs}_{max720p}_{audioOnly}_{language}",
                async () => {
                    var manifest = await GetManifestAsync(manifestUrl);
                    return await CustomizeTopLevelManifestAsync(
                        getSecondLevelProxyUrl,
                        getSubtitleProxyUrl,
                        manifest,
                        manifestUrl,
                        subs,
                        max720p,
                        audioOnly,
                        language);
                },
                DateTimeOffset.UtcNow.AddSeconds(30)
            );

            return manifest.Replace("TOKENPLACEHOLDER", token);
        }


        public async Task<string> ProxySecondLevelAsync(
            string manifestUrl,
            string token)
        {
            var manifest = await GetManifestAsync(manifestUrl);
            return CustomizeSecondLevelManifestAsync(manifest, manifestUrl, token);
        }

        private async Task<string> CustomizeTopLevelManifestAsync(
            Func<(string url, string token), string> getSecondLevelProxyUrl,
            Func<string, string> getSubtitleProxyUrl,
            string manifest,
            string manifestUrl,
            bool subs,
            bool max720p,
            bool audioOnly,
            string language
        )
        {
            var videoFilename = Regex.Match(manifestUrl, @"[^\/]*?(?=\.ism\/)")?.Value;
            manifest = ConvertRelativeUrlsToAbsolute(manifest, manifestUrl);
            manifest = ConvertUrlsToProxyUrls(getSecondLevelProxyUrl, manifest);
            manifest = SetAudioDefaults(manifest);

            if (max720p)
            {
                manifest = RemoveQualityLevel(manifest, "1920x1080");
            }

            if (subs && !string.IsNullOrWhiteSpace(videoFilename))
            {
                manifest = await AddSubtitlesAsync(getSubtitleProxyUrl, manifest, videoFilename);
            }

            if (audioOnly)
            {
                manifest = ModifyManifestToBeAudioOnly(manifest, language);
            }

            return manifest;
        }

        private static string CustomizeSecondLevelManifestAsync(string manifest, string manifestUrl, string token)
        {
            manifest = AddTokenToKeyDelivery(manifest, token);
            manifest = ConvertRelativeUrlsToAbsolute(manifest, manifestUrl);
            return manifest;
        }

        private static string ConvertUrlsToProxyUrls(
            Func<(string url, string token), string> getSecondLevelProxyUrl,
            string manifest)
        {
            manifest = Regex.Replace(manifest, @"^https:\/\/vod\.brunstad\.tv\/.+$", m => getSecondLevelProxyUrl((m.Value, "TOKENPLACEHOLDER")), RegexOptions.Multiline);
            manifest = Regex.Replace(manifest, @"URI=""(https:\/\/vod\.brunstad\.tv.+?)""", m => "URI=\"" + getSecondLevelProxyUrl((m.Groups[1]?.Value, "TOKENPLACEHOLDER")) + "\"");
            return manifest;
        }

        private async Task<string> AddSubtitlesAsync(
            Func<string, string> getSubtitleProxyUrl,
            string manifest,
            string videoFilename)
        {
            var subs = await _subtitleService.GetCachedByVideoFileName(videoFilename);
            var hasSubs = false;

            foreach (var sub in subs.Where(s => s.Type == "vtt"))
            {
                hasSubs = true;
                manifest += $"#EXT-X-MEDIA:TYPE=SUBTITLES,NAME=\"{sub.Label}\",DEFAULT=NO,AUTOSELECT=NO,FORCED=NO,LANGUAGE=\"{sub.LanguageCode}\",GROUP-ID=\"subs\",URI=\"{getSubtitleProxyUrl(sub.Url)}\"\n";
            }

            if (hasSubs)
            {
                manifest = manifest.Replace("AUDIO=\"audio\"", "AUDIO=\"audio\",SUBTITLES=\"subs\"");
            }

            return manifest;
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

        private string ModifyManifestToBeAudioOnly(string topLevelManifest, string language)
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

        private static string ConvertRelativeUrlsToAbsolute(string manifest, string manifestUrl)
        {
            var baseUrl = GetAbsoluteBaseUrl(manifestUrl);
            // https://datatracker.ietf.org/doc/html/draft-pantos-hls-rfc8216bis#section-4.1
            // Lines not starting with # is a file/playlist url.
            // If relative, make it absolute.
            manifest = Regex.Replace(manifest, @"^(?!https?:\/\/)[^#\s].+", baseUrl + "/$&", RegexOptions.Multiline);
            manifest = Regex.Replace(manifest, @"URI=""(?!https?:\/\/)(.+?)""", $"URI=\"{baseUrl}/$1\"");
            return manifest;
        }

        private static string GetAbsoluteBaseUrl(string url)
        {
            return url.Substring(0, url.LastIndexOf("/"));
        }

        private static string AddTokenToKeyDelivery(string manifest, string token)
        {
            return Regex.Replace(
                manifest,
                @"^#EXT-X-KEY:METHOD=AES-128,URI="".+?(?="")",
                m => m.Value + "&token=" + HttpUtility.UrlEncode(token),
                RegexOptions.Multiline
            );
        }

        public string GenerateSubtitleManifest(string fileUrl)
        {
            return $"#EXTM3U\n#EXT-X-TARGETDURATION:10000\n#EXT-X-VERSION:4\n#EXT-X-MEDIA-SEQUENCE:1\n#EXT-X-PLAYLIST-TYPE:VOD\n#EXTINF:10000.0,\n{fileUrl}\n#EXT-X-ENDLIST\n";
        }

        private async Task<string> GetManifestAsync(string uri)
        {
            return await _appCache.GetOrAddAsync(
                "manifest_" + uri.ToLowerInvariant(),
                async () => await GetRawContentsAsync(uri),
                DateTimeOffset.UtcNow.AddSeconds(30)
            );
        }

        private async Task<string> GetRawContentsAsync(string uri)
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, uri);

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            var response = await client.SendAsync(httpRequest);
            return await response.Content.ReadAsStringAsync();
        }
    }
}
