using LivestreamFunctions.Model;
using LivestreamFunctions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using Microsoft.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Linq;

namespace LivestreamFunctions
{
    [ApiController]
    [Route("api/hls-proxy")]
    public class HlsProxyController : ControllerBase
    {
        private readonly StreamingTokenHelper _streamingTokenHelper;
        private readonly HlsProxyService _proxyService;
        private readonly LivestreamOptions _liveOptions;

        public HlsProxyController(StreamingTokenHelper streamingTokenHelper, HlsProxyService proxyService, IOptions<LivestreamOptions> options)
        {
            _streamingTokenHelper = streamingTokenHelper;
            _proxyService = proxyService;
            _liveOptions = options.Value;
        }

        [HttpHead("subtitles")]
        [HttpHead("top-level")]
        [HttpHead("second-level")]
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

        [HttpGet("top-level")]
        public async Task<IActionResult> GetTopLevelManifest(string token, string playback_url = null, bool audio_only = false, string language = null)
        {
            if (string.IsNullOrEmpty(token))
            {
                return new BadRequestObjectResult("Missing parameters");
            }

            Uri uri;
            if (!Uri.TryCreate(playback_url, UriKind.Absolute, out uri))
            {
                return BadRequest("Bad uri: " + playback_url);
            }

            if (!_liveOptions.GetAllowedHosts().Contains(uri.Host.ToLower()))
            {
                return BadRequest("Host not allowed: " + uri.Host);
            }

            var secondLevelProxyUrl = Url.Action("GetSecondLevelManifest", null, null, Request.Scheme); ;

            var manifest = await _proxyService.RetrieveAndModifyTopLevelManifestForToken(playback_url ?? _liveOptions.HlsUrl, token, secondLevelProxyUrl);
            if (audio_only)
            {
                manifest = _proxyService.ModifyManifestToBeAudioOnly(manifest, language);
            }

            Response.Headers.Add("X-Content-Type-Options", "nosniff");
            Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
            {
                NoStore = true,
                MaxAge = TimeSpan.FromSeconds(0)
            };
            return Content(manifest, "application/vnd.apple.mpegurl", Encoding.UTF8);
        }

        [HttpGet("second-level")]
        public async Task<IActionResult> GetSecondLevelManifest(string token, string url)
        {
            var allowedHost = new Uri(_liveOptions.HlsUrl).Host.ToLower();
            var host = new Uri(url).Host.ToLower();
            if (host != allowedHost)
            {
                return new BadRequestObjectResult("Invalid url, only URLS with '" + allowedHost + "' as host are allowed.");
            }
            if (string.IsNullOrEmpty(token))
            {
                return new BadRequestObjectResult("Missing parameters");
            }

            var keyDeliveryBaseUrl = $"{Request.Scheme}://{Request.Host}/api/keydelivery";
            var urlEncodedToken = HttpUtility.UrlEncode(token);
            string generateKeyDeliveryUrl(string group, string keyId)
            {
                return keyDeliveryBaseUrl + $"/{group}/{keyId}?token={urlEncodedToken}";
            }

            var manifest = await _proxyService.RetrieveAndModifySecondLevelManifestAsync(url, generateKeyDeliveryUrl);

            Response.Headers.Add("X-Content-Type-Options", "nosniff");
            Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
            {
                Public = true,
                MaxAge = TimeSpan.FromSeconds(0)
            };
            return Content(manifest, "application/vnd.apple.mpegurl", Encoding.UTF8);
        }


    }
}
