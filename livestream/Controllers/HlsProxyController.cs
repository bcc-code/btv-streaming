using LivestreamFunctions.Services;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace LivestreamFunctions
{
    [ApiController]
    [Route("hls-proxy")]
    public class HlsProxyController : ControllerBase
    {
        private readonly StreamingTokenHelper _streamingTokenHelper;
        private readonly ProxyService _proxyService;
         
        public HlsProxyController(StreamingTokenHelper streamingTokenHelper, ProxyService proxyService)
        {
            _streamingTokenHelper = streamingTokenHelper;
            _proxyService = proxyService;
        }

        [HttpGet("top-level")]
        [EnableCors("All")]
        public async Task<IActionResult> GetTopLevelManifest(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return new BadRequestObjectResult("Missing parameters");
            }

            if (!_streamingTokenHelper.ValidateToken(token))
            {
                return new UnauthorizedResult();
            }

            throw new NotImplementedException();
        }


    }
}
