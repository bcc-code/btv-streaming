using LivestreamFunctions.Model;
using LivestreamFunctions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace LivestreamFunctions
{
    [ApiController]
    [Route("api/urls")]
    [Authorize]
    public class UrlController : ControllerBase
    {
        private readonly StreamingTokenHelper _streamingTokenHelper;
        private readonly IOptions<LivestreamOptions> _liveOptions;

        public UrlController(StreamingTokenHelper streamingTokenHelper, IOptions<LivestreamOptions> liveOptions)
        {
            _streamingTokenHelper = streamingTokenHelper;
            _liveOptions = liveOptions;
        }

        [HttpGet("live")]
        [EnableCors("All")]
        public ActionResult<UrlDto> GetHls()
        {
            var expiryTime = DateTimeOffset.UtcNow.AddHours(6);
            var token = _streamingTokenHelper.Generate(expiryTime);

            return new UrlDto {
                Url = Url.Action("GetTopLevelManifest", "CmafProxy", null, Request.Scheme) + "?url=" + HttpUtility.UrlEncode(_liveOptions.Value.HlsUrl2) + "&token=" + token,
                ExpiryTime = expiryTime
            };
        }

        [HttpGet("live-audio")]
        [EnableCors("All")]
        public ActionResult<UrlDto> GetLiveAudioOnlyUrl(string language = null)
        {
            var url = Url.Action("GetTopLevelManifest", "HlsProxy", null, Request.Scheme);
            url += "?audio_only=true&someParam=.m3u8";

            if (!string.IsNullOrWhiteSpace(language))
            {
                language = new string(language.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
                url += $"&language={language}";
            }
            
            var expiryTime = DateTimeOffset.UtcNow.AddHours(6);
            var streamingToken = _streamingTokenHelper.Generate(expiryTime);
            url += $"&token={streamingToken}";

            return new UrlDto {
                Url = _liveOptions.Value.HlsUrl2,
                ExpiryTime = expiryTime
            };
        }
    }

}
