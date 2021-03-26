using LivestreamFunctions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
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
        private readonly string _topLevelManifestBaseURL = "https://btvliveproxy.azurewebsites.net/api/TopLevelManifest";

        public UrlController(StreamingTokenHelper streamingTokenHelper)
        {
            _streamingTokenHelper = streamingTokenHelper;
        }

        [HttpGet("live")]
        [EnableCors("All")]
        public async Task<IActionResult> GetHls()
        {
            string streamingToken = Request.Query["token"];
            if (!string.IsNullOrEmpty(streamingToken))
            {
                streamingToken = new string(streamingToken.Where(c => char.IsLetterOrDigit(c) || c == '.' || c == '_').ToArray());
            }
            else
            {
                streamingToken = _streamingTokenHelper.Generate();
            }

            return new OkObjectResult(new { url = _topLevelManifestBaseURL + "?token=" + streamingToken });
        }

        [HttpGet("live-audio")]
        [EnableCors("All")]
        public async Task<IActionResult> GetLiveAudioOnlyUrl()
        {
            string language = Request.Query["language"];
            var url = _topLevelManifestBaseURL + "?audio_only=true&someParam=.m3u8";

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
