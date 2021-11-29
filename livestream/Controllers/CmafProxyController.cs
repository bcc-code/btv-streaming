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
using Microsoft.AspNetCore.Authorization;

namespace LivestreamFunctions
{
    [ApiController]
    [Route("api/cmaf-proxy")]
    public class CmafProxyController : ControllerBase
    {
        private readonly StreamingTokenHelper _streamingTokenHelper;
        private readonly CmafProxyService _proxyService;
        private readonly UrlSigner _urlSigner;
        private readonly LivestreamOptions _liveOptions;

        public CmafProxyController(StreamingTokenHelper streamingTokenHelper,
            CmafProxyService proxyService,
            IOptions<LivestreamOptions> options,
            UrlSigner urlSigner)
        {
            _streamingTokenHelper = streamingTokenHelper;
            _proxyService = proxyService;
            _urlSigner = urlSigner;
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
                return BadRequest("Missing parameters");
            }

            if (string.IsNullOrEmpty(url))
            {
                if (_streamingTokenHelper.ValidateToken(token))
                {
                    url = _urlSigner.Sign(_liveOptions.HlsUrl2);
                } else
                {
                    return BadRequest("Missing parameters");
                }
            }

            var secondLevelProxyUrl = Url.Action("GetSecondLevelManifest", null, null, Request.Scheme);

            var manifest = await _proxyService.RetrieveAndModifyTopLevelManifestForToken(url, secondLevelProxyUrl);
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
        public async Task<IActionResult> GetSecondLevelManifest(string url)
        {
            var keyDeliveryBaseUrl = $"{Request.Scheme}://{Request.Host}/api/keydelivery";

            var manifest = await _proxyService.RetrieveAndModifySecondLevelManifestAsync(url);

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
