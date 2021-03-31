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
        public IActionResult GetHls(string token = null)
        {
            if (!string.IsNullOrEmpty(token))
            {
                token = new string(token.Where(c => char.IsLetterOrDigit(c) || c == '.' || c == '_').ToArray());
            }
            else
            {
                token = _streamingTokenHelper.Generate();
            }

            return new OkObjectResult(new { url = Url.Action("GetTopLevelManifest", "HlsProxy", null, Request.Scheme) + "?token=" + token });
        }

        [HttpGet("live-audio")]
        [EnableCors("All")]
        public IActionResult GetLiveAudioOnlyUrl(string language = null)
        {
            var url = Url.Action("GetTopLevelManifest", "HlsProxy", null, Request.Scheme);
            url += "?audio_only=true&someParam=.m3u8";

            if (!string.IsNullOrWhiteSpace(language))
            {
                language = new string(language.Where(char.IsLetterOrDigit).ToArray());
                url += $"&language={language}";
            }

            var streamingToken = _streamingTokenHelper.Generate();
            url += $"&token={streamingToken}";
            return new OkObjectResult(new { url });
        }
    }

}
