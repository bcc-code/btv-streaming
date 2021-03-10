using LivestreamFunctions.Services;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;

namespace LivestreamFunctions
{
    [ApiController]
    [Route("KeyDelivery")]
    public class KeyDelivery : ControllerBase
    {
        private readonly StreamingTokenHelper _streamingTokenHelper;
        private readonly KeyRepository _keyRepository;

        public KeyDelivery(StreamingTokenHelper streamingTokenHelper, KeyRepository keyRepository)
        {
            _streamingTokenHelper = streamingTokenHelper;
            _keyRepository = keyRepository;
        }

        [HttpGet("{keyGroup}/{keyId}")]
        [EnableCors("All")]
        public async Task<IActionResult> Run(
            string keyGroup,
            string keyId
            )
        {
            string token = Request.Query["token"];
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
    }
}
