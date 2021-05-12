using VODFunctions.Services;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace VODFunctions
{
    [ApiController]
    [Route("api/keydelivery")]
    public class KeyController : ControllerBase
    {
        private readonly StreamingTokenHelper _streamingTokenHelper;
        private readonly KeyRepository _keyRepository;

        public KeyController(StreamingTokenHelper streamingTokenHelper, KeyRepository keyRepository)
        {
            _streamingTokenHelper = streamingTokenHelper;
            _keyRepository = keyRepository;
        }

        [HttpGet("{keyGroup}/{keyId}")]
        [EnableCors("All")]
        public async Task<IActionResult> GetByKeyAndGroup(string keyGroup, string keyId, string token)
        {
            if (string.IsNullOrEmpty(keyGroup) || string.IsNullOrEmpty(keyId) || string.IsNullOrEmpty(token))
            {
                return new BadRequestObjectResult("Missing parameters");
            }

            if (!_streamingTokenHelper.ValidateToken(token))
            {
                return new UnauthorizedResult();
            }

            var keyBytes = await _keyRepository.GetKeyAsync(keyGroup, keyId);
            var stream = new MemoryStream(keyBytes);
            return new FileStreamResult(stream, "binary/octet-stream");
        }

        [HttpPost]
        [HttpGet]
        [Route("/api/widevine/getLicense")]
        [EnableCors("All")]
        public async Task<IActionResult> GetLicense(string token)
        {
            var json = await new StreamReader(Request.Body).ReadToEndAsync();
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(token))
            {
                return new BadRequestObjectResult("Missing parameters");
            }

            if (!_streamingTokenHelper.ValidateToken(token))
            {
                return new UnauthorizedResult();
            }

            // Spec about the request body: https://www.w3.org/TR/encrypted-media/#x9.1.3-license-request-format
            var body = JsonConvert.DeserializeAnonymousType(json, new { kids = new List<string>() });
            var kidByteArray = Base64UrlEncoder.DecodeBytes(body.kids[0]);
            var kidHex = BitConverter.ToString(kidByteArray).Replace("-", "");
            var kidGuid = Guid.Parse(kidHex);
            var kid = kidGuid.ToString();
            var keyBytes = await _keyRepository.GetKeyWithDASHGroupAsync(kid);

            var jsonWebKey = new JsonWebKey
            {
                Kty = "oct",
                K = Base64UrlEncoder.Encode(keyBytes),
                Kid = body.kids[0]
            };

            var jwks = new JsonWebKeySet();
            jwks.Keys.Add(jsonWebKey);

            return new OkObjectResult(jwks);
        }
    }
}
