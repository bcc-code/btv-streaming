using VODFunctions.Model;
using VODFunctions.Services;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using Microsoft.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace VODFunctions
{
    [ApiController]
    [Route("api/hls-proxy")]
    public class HlsProxyController : ControllerBase
    {
        private readonly StreamingTokenHelper _streamingTokenHelper;
        private readonly HlsProxyService _proxyService;
        private readonly VODOptions _liveOptions;

        public HlsProxyController(StreamingTokenHelper streamingTokenHelper, HlsProxyService proxyService, IOptions<VODOptions> options)
        {
            _streamingTokenHelper = streamingTokenHelper;
            _liveOptions = options.Value;
        }

        [HttpGet("/api/vod/toplevelmanifest")]
        [HttpGet("top-level")]
        [EnableCors("All")]
        public async Task<IActionResult> GetTopLevelManifest(string playbackUrl, string token, bool max720p = false, bool audio_only = false, string language = null)
        {
            if (string.IsNullOrWhiteSpace(playbackUrl) || string.IsNullOrWhiteSpace(token))
            {
                return new BadRequestObjectResult("Missing parameters");
            }

            if (!_streamingTokenHelper.ValidateToken(token))
            {
                return new UnauthorizedResult();
            }

            var secondLevelProxyUrl = Url.Action("GetSecondLevelManifest", null, null, Request.Scheme);

            var manifest = await _proxyService.RetrieveAndModifyTopLevelManifestForToken(playbackUrl, token, secondLevelProxyUrl);
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
        public async Task<IActionResult> GetSecondLevelManifest(string playbackUrl, string token)
        {
            if (string.IsNullOrWhiteSpace(playbackUrl) || string.IsNullOrWhiteSpace(token))
            {
                return new BadRequestObjectResult("Missing parameters");
            }

            if (!_streamingTokenHelper.ValidateToken(token))
            {
                return new UnauthorizedResult();
            }

            var allowedHost = new Uri(_liveOptions.HlsUrl).Host.ToLower();
            var host = new Uri(playbackUrl).Host.ToLower();
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

            var manifest = await _proxyService.RetrieveAndModifySecondLevelManifestAsync(playbackUrl, token);

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
