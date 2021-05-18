using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using VODFunctions.Model;
using VODFunctions.Services;

namespace VODFunctions
{
    [ApiController]
    [Route("api/vod")]
    public class HlsProxyController : ControllerBase
    {
        private readonly StreamingTokenHelper _streamingTokenHelper;
        private readonly HlsProxyService _proxyService;
        private readonly VODOptions _vodOptions;

        public HlsProxyController(StreamingTokenHelper streamingTokenHelper,
            HlsProxyService proxyService,
            IOptions<VODOptions> options)
        {
            _streamingTokenHelper = streamingTokenHelper;
            _proxyService = proxyService;
            _vodOptions = options.Value;
        }

        [HttpGet("toplevelmanifest")]
        [EnableCors("All")]
        public async Task<IActionResult> GetTopLevelManifest(string playbackUrl,
            string token,
            bool subs = true,
            bool max720p = false,
            bool audioOnly = false,
            string language = null)
        {
            if (string.IsNullOrWhiteSpace(playbackUrl) || string.IsNullOrWhiteSpace(token))
            {
                return new BadRequestObjectResult("Missing parameters");
            }

            var allowedHost = "vod.brunstad.tv";
            var host = new Uri(playbackUrl).Host.ToLower();
            if (host != allowedHost)
            {
                return new BadRequestObjectResult("Invalid url, host not allowed.");
            }

            token = new string(token.Where(c => char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-').ToArray());

            var manifest = await _proxyService.ProxyTopLevelAsync(
                GetSecondLevelProxyUrl,
                GetSubtitleManifestUrl,
                playbackUrl,
                token,
                subs,
                max720p,
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

            var allowedHost = "vod.brunstad.tv";
            var host = new Uri(playbackUrl).Host.ToLower();
            if (host != allowedHost)
            {
                return new BadRequestObjectResult("Invalid url, host not allowed.");
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
