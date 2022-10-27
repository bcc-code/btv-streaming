using LivestreamFunctions.Model;
using LivestreamFunctions.Services;
using Microsoft.AspNetCore.Authorization;
using System.Web;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LivestreamFunctions
{
    [ApiController]
    [Route("api/urls")]
    [Authorize]
    public class UrlController : ControllerBase
    {
        private readonly StreamingTokenHelper _streamingTokenHelper;
        private readonly IOptions<LivestreamOptions> _liveOptions;
        private readonly UrlSigner _urlSigner;

        public UrlController(StreamingTokenHelper streamingTokenHelper, IOptions<LivestreamOptions> liveOptions, UrlSigner urlSigner)
        {
            _streamingTokenHelper = streamingTokenHelper;
            _liveOptions = liveOptions;
            _urlSigner = urlSigner;
        }

        [HttpOptions("live")]
        [HttpHead("live")]
        [HttpGet("live")]
        [EnableCors("All")]
        public ActionResult<UrlDto> GetHls(string experiment = null)
        {
            if (_liveOptions.Value.UsePureUrl) {
                return new UrlDto {
                    Url = _liveOptions.Value.PureUrl,
                    ExpiryTime = DateTimeOffset.UtcNow.AddHours(6)
                };
            }
            
            var expiryTime = DateTimeOffset.UtcNow.AddHours(6);
            var token = _streamingTokenHelper.Generate(expiryTime);

            string url;
            if (experiment?.ToLower() == "v1")
            {
                var signedUrl = _urlSigner.Sign(_liveOptions.Value.HlsUrl2);
                url = Url.Action("GetTopLevelManifest", "CmafProxy", null, Request.Scheme)
                    + "?url="
                    + HttpUtility.UrlEncode(signedUrl)
                    + "&token="
                    + token;
            } else
            {
                url = _urlSigner.Sign(_liveOptions.Value.HlsUrlCmafV2);
                url = _liveOptions.Value.HlsUrlCmafV2
                + "?EncodedPolicy="
                + HttpUtility.UrlEncode(url.Substring(url.IndexOf("?Policy")+1))
                + "&token="
                + token;
            }

            return new UrlDto {
                Url = url,
                ExpiryTime = expiryTime
            };
        }

        [HttpOptions("live-audio")]
        [HttpHead("live-audio")]
        [HttpGet("live-audio")]
        [EnableCors("All")]
        public ActionResult<UrlDto> GetLiveAudioOnlyUrl(string language = null)
        {
            var url = Url.Action("GetTopLevelManifest", "CmafProxy", null, Request.Scheme);
            url += "?audio_only=true&someParam=.m3u8";

            if (!string.IsNullOrWhiteSpace(language))
            {
                language = new string(language.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
                url += $"&language={language}";
            }

            url += "&url=" + HttpUtility.UrlEncode(_urlSigner.Sign(_liveOptions.Value.HlsUrl2));

            var expiryTime = DateTimeOffset.UtcNow.AddHours(6);
            var streamingToken = _streamingTokenHelper.Generate(expiryTime);
            url += $"&token={streamingToken}";

            return new UrlDto {
                Url = url,
                ExpiryTime = expiryTime
            };
        }
    }

}
