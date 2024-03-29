using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using VODStreaming.Model;
using VODStreaming.Services;

namespace VODStreaming
{
    [ApiController]
    [Route("api/vod")]
    public class HlsProxyController : ControllerBase
    {
        private readonly HlsProxyService _proxyService;
        private readonly VODOptions _vodOptions;

        public HlsProxyController(HlsProxyService proxyService, IOptions<VODOptions> options)
        {
            _proxyService = proxyService;
            _vodOptions = options.Value;
        }

        [HttpHead("subtitles")]
        [HttpHead("toplevelmanifest")]
        [HttpHead("secondlevelmanifest")]
        [EnableCors("All")]
        public ActionResult GetHeadersForHeadRequests()
        {
            Response.Headers.Add("X-Content-Type-Options", "nosniff");
            Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
            {
                NoStore = true,
                MaxAge = TimeSpan.FromSeconds(0)
            };
            return Content("", "application/vnd.apple.mpegurl", Encoding.UTF8);
        }

        [HttpGet("toplevelmanifest")]
        [EnableCors("All")]
        public async Task<IActionResult> GetTopLevelManifest(string playbackUrl,
            string token,
            bool subs = true,
            bool max720p = false,
            bool audioOnly = false,
            bool removeAudioOnlyTrack = false,
            string language = null)
        {
            if (string.IsNullOrWhiteSpace(playbackUrl) || string.IsNullOrWhiteSpace(token))
            {
                return new BadRequestObjectResult("Missing parameters");
            }

            var ua = Request.Headers["User-Agent"].ToString();
            if (!string.IsNullOrEmpty(ua) && ua.Contains("Apple TV")) // tvOs
            {
                removeAudioOnlyTrack = true;
            }

            var allowedHosts = new string[]{ "vod.brunstad.tv", "vod2.brunstad.tv", "vod-dev.stream.brunstad.tv", "vod-sta.stream.brunstad.tv", "vod.stream.brunstad.tv"};
            Uri.TryCreate(playbackUrl, UriKind.Absolute, out var uri);
            var host = uri?.Host.ToLower();
            if (host == null || !allowedHosts.Contains(host))
            {
                return new BadRequestObjectResult("Invalid url or host not allowed.");
            }

            token = new string(token.Where(c => char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-').ToArray());

            var manifest = await _proxyService.ProxyTopLevelAsync(
                GetSecondLevelProxyUrl,
                GetSubtitleManifestUrl,
                playbackUrl,
                token,
                subs,
                max720p,
                removeAudioOnlyTrack,
                audioOnly,
                language);

            Response.Headers.Add("X-Content-Type-Options", "nosniff");
            Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
            {
                NoStore = true,
                MaxAge = TimeSpan.FromSeconds(0)
            };
            return Content(manifest, "application/vnd.apple.mpegurl", Encoding.UTF8);
        }

        public string GetSecondLevelProxyUrl((string originalUrl, string token) args)
        {
            var secondLevelProxyUrl = Url.Action(nameof(GetSecondLevelManifest), null, null, Request.Scheme);
            return $"{secondLevelProxyUrl}?playbackUrl={HttpUtility.UrlEncode(args.originalUrl)}&token={HttpUtility.UrlEncode(args.token)}";
        }

        [HttpGet("secondlevelmanifest")]
        [EnableCors("All")]
        public async Task<IActionResult> GetSecondLevelManifest(string playbackUrl, string token)
        {
            if (string.IsNullOrWhiteSpace(playbackUrl) || string.IsNullOrWhiteSpace(token))
            {
                return new BadRequestObjectResult("Missing parameters");
            }

            var allowedHosts = new string[]{ "vod.brunstad.tv", "vod2.brunstad.tv", "vod-dev.stream.brunstad.tv", "vod-sta.stream.brunstad.tv", "vod.stream.brunstad.tv"};
            Uri.TryCreate(playbackUrl, UriKind.Absolute, out var uri);
            var host = uri?.Host.ToLower();
            if (host == null || !allowedHosts.Contains(host))
            {
                return new BadRequestObjectResult("Invalid url or host not allowed.");
            }

            token = new string(token.Where(c => char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-').ToArray());

            var manifest = await _proxyService.ProxySecondLevelAsync(playbackUrl, token);

            Response.Headers.Add("X-Content-Type-Options", "nosniff");
            Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
            {
                NoStore = true,
                MaxAge = TimeSpan.FromSeconds(0),
            };
            return Content(manifest, "application/vnd.apple.mpegurl", Encoding.UTF8);
        }

        public string GetSubtitleManifestUrl(string fileUrl)
        {
            var secondLevelProxyUrl = Url.Action(nameof(GetSubtitleManifest), null, null, Request.Scheme);
            return $"{secondLevelProxyUrl}?subs={HttpUtility.UrlEncode(fileUrl)}";
        }

        [HttpGet("subtitles")]
        [EnableCors("All")]
        public IActionResult GetSubtitleManifest(string subs)
        {
            if (string.IsNullOrWhiteSpace(subs))
            {
                return new BadRequestObjectResult("Missing parameters");
            }

            var manifest = _proxyService.GenerateSubtitleManifest(subs);

            Response.Headers.Add("X-Content-Type-Options", "nosniff");
            Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
            {
                NoStore = false,
                MaxAge = TimeSpan.FromSeconds(60)
            };
            return Content(manifest, "application/vnd.apple.mpegurl", Encoding.UTF8);
        }

    }
}
