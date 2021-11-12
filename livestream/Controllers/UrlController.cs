using LivestreamFunctions.Model;
using LivestreamFunctions.Services;
using Microsoft.AspNetCore.Authorization;
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

        public UrlController(StreamingTokenHelper streamingTokenHelper, IOptions<LivestreamOptions> liveOptions)
        {
            _streamingTokenHelper = streamingTokenHelper;
        }

        [HttpGet("live")]
        [EnableCors("All")]
        public ActionResult<UrlDto> GetHls(string token = null)
        {
            var url = Url.Action("GetTopLevelManifest", "HlsProxy", null, Request.Scheme);
            url += "?special_mode=true&someParam=.m3u8";

            url += $"&language=nor";

            var expiryTime = DateTimeOffset.UtcNow.AddHours(6);
            var streamingToken = _streamingTokenHelper.Generate(expiryTime);
            url += $"&token={streamingToken}";

            return new UrlDto
            {
                Url = url,
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
                Url = url,
                ExpiryTime = expiryTime
            };
        }
    }

}
