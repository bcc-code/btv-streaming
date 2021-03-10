using LivestreamFunctions.Services;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LivestreamFunctions
{
    [ApiController]
    [Route("urls")]
    public class GetStreamUrl : ControllerBase
    {
        public readonly OAuthValidator _oAuthValidator;
        private readonly StreamingTokenHelper _streamingTokenHelper;
        private readonly string _topLevelManifestBaseURL = "https://btvliveproxy.azurewebsites.net/api/TopLevelManifest";

        public GetStreamUrl(OAuthValidator oAuthValidator, StreamingTokenHelper streamingTokenHelper)
        {
            _oAuthValidator = oAuthValidator;
            _streamingTokenHelper = streamingTokenHelper;
        }

        [HttpGet("live")]
        [EnableCors("All")]
        public async Task<IActionResult> Get()
        {
            var jwt = Request.Headers["Authorization"].ToString().Replace("Bearer ", "", StringComparison.InvariantCultureIgnoreCase);
            if (!await _oAuthValidator.ValidateToken(jwt))
            {
                return new UnauthorizedResult();
            }

            string streamingToken = Request.Query["token"];
            if (!string.IsNullOrEmpty(streamingToken))
            {
                streamingToken = new string(streamingToken.Where(c => char.IsLetterOrDigit(c) || c == '.' || c == '_').ToArray());
                // do country check
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
            var jwt = Request.Headers["Authorization"].ToString().Replace("Bearer ", "", StringComparison.InvariantCultureIgnoreCase);
            if (!await _oAuthValidator.ValidateToken(jwt))
            {
                return new UnauthorizedResult();
            }

            var url = _topLevelManifestBaseURL + "?audio_only=true";
            url += "&someParam=.m3u8";

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
