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

        [HttpGet("top-level")]
        [EnableCors("All")]
        public async Task<IActionResult> GetTopLevelManifest(string token, bool audio_only = false, bool special_mode = false, string language = null)
        {
            if (special_mode)
            {
                Response.Headers.Add("X-Content-Type-Options", "nosniff");
                Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
                {
                    NoStore = true,
                    MaxAge = TimeSpan.FromSeconds(0)
                };
                return Content("#EXTM3U\n#EXT-X-VERSION:7\n#EXT-X-INDEPENDENT-SEGMENTS\n#EXT-X-STREAM-INF:BANDWIDTH=128000,AUDIO=\"audio_0\",CODECS=\"avc1.42C00C,mp4a.40.2\"\nhttps://live.stream.brunstad.tv/out/v1/37f897a9330c454ebc1b6a983495e3a8/index_1.m3u8\n#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=\"audio_0\",CHANNELS=\"2\",NAME=\"Norsk\",LANGUAGE=\"nor\",DEFAULT=YES,AUTOSELECT=YES,URI=\"https://live.stream.brunstad.tv/out/v1/37f897a9330c454ebc1b6a983495e3a8/index_7_0.m3u8\"\n", "application/vnd.apple.mpegurl", Encoding.UTF8);
            }

            if (string.IsNullOrEmpty(token))
            {
                return new BadRequestObjectResult("Missing parameters");
            }

            var secondLevelProxyUrl = Url.Action("GetSecondLevelManifest", null, null, Request.Scheme); ;

            var manifest = await _proxyService.RetrieveAndModifyTopLevelManifestForToken(_liveOptions.HlsUrl, token, secondLevelProxyUrl);
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
            /*var allowedHost = new Uri(_liveOptions.HlsUrl).Host.ToLower();
            var host = new Uri(url).Host.ToLower();
            if (host != allowedHost)
            {
                return new BadRequestObjectResult("Invalid url, only URLS with '" + allowedHost + "' as host are allowed.");
            }*/
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
