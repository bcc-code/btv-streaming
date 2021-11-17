using LivestreamFunctions.Model;
using LivestreamFunctions.Services;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using Microsoft.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace LivestreamFunctions
{
    [ApiController]
    [Route("api/cmaf-proxy")]
    public class CmafProxyController : ControllerBase
    {
        private readonly StreamingTokenHelper _streamingTokenHelper;
        private readonly CmafProxyService _proxyService;
        private readonly LivestreamOptions _liveOptions;

        public CmafProxyController(StreamingTokenHelper streamingTokenHelper, CmafProxyService proxyService, IOptions<LivestreamOptions> options)
        {
            _streamingTokenHelper = streamingTokenHelper;
            _proxyService = proxyService;
            _liveOptions = options.Value;
        }

        [HttpHead("subtitles")]
        [HttpHead("top-level")]
        [HttpHead("second-level")]
        public ActionResult GetHeadersForHeadRequests()
        {
            Response.Headers.Add("Access-Control-Allow-Origin", "*");
            Response.Headers.Add("Access-Control-Allow-Methods", "GET");
            Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
            Response.Headers.Add("X-Content-Type-Options", "nosniff");
            Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
            {
                NoStore = true,
                MaxAge = TimeSpan.FromSeconds(0)
            };
            return Content("", "application/vnd.apple.mpegurl", Encoding.UTF8);
        }

        [HttpGet("top-level")]
        [EnableCors("All")]
        public async Task<IActionResult> GetTopLevelManifest(string token, string url, bool audio_only = false, string language = null)
        {
            if (string.IsNullOrEmpty(token))
            {
                return new BadRequestObjectResult("Missing parameters");
            }

            var secondLevelProxyUrl = Url.Action("GetSecondLevelManifest", null, null, Request.Scheme);

            var manifest = await _proxyService.RetrieveAndModifyTopLevelManifestForToken(url, token, secondLevelProxyUrl);
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
        [EnableCors("All")]
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
