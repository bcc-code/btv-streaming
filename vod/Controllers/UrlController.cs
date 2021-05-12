using VODFunctions.Model;
using VODFunctions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace VODFunctions
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
            DateTimeOffset? expiryTime = null;
            if (!string.IsNullOrEmpty(token))
            {
                token = new string(token.Where(c => char.IsLetterOrDigit(c) || c == '.' || c == '_').ToArray());
            }
            else
            {
                expiryTime = DateTimeOffset.UtcNow.AddHours(6);
                token = _streamingTokenHelper.Generate(expiryTime.Value);
            }

            return new UrlDto {
                Url = Url.Action("GetTopLevelManifest", "HlsProxy", null, Request.Scheme) + "?token=" + token,
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
                language = new string(language.Where(char.IsLetterOrDigit).ToArray());
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
