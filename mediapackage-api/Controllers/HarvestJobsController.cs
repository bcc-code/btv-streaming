using System;
using System.Threading.Tasks;
using Amazon.MediaPackage;
using Amazon.MediaPackage.Model;
using Amazon.S3;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MediaPackageAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class HarvestJobsController : ControllerBase
    {
        private readonly ILogger<HarvestJobsController> _logger;
        private readonly IAmazonMediaPackage _mediaPackageClient;
        private readonly IAmazonS3 _s3Client;
        private readonly IConfiguration _configuration;

        public HarvestJobsController(
            ILogger<HarvestJobsController> logger,
            IAmazonMediaPackage mediaPackageClient,
            IAmazonS3 s3Client,
            IConfiguration configuration)
        {
            _logger = logger;
            _mediaPackageClient = mediaPackageClient;
            _s3Client = s3Client;
            _configuration = configuration;
        }

        [HttpPost("compliance/new")]
        public async Task<ActionResult> ComplianceRecording()
        {
            var request = new CreateHarvestJobRequest
            {
                Id = DateTimeOffset.UtcNow.ToString(("yyyy-MM-ddTHH-mm")),
                StartTime = DateTimeOffset.UtcNow.AddMinutes(-6).ToString("O"),
                EndTime = DateTimeOffset.UtcNow.ToString("O"),
                OriginEndpointId = _configuration["MediaPackageEndpointId"],
                S3Destination = new S3Destination
                {
                    BucketName = _configuration["S3BucketName"],
                    RoleArn = _configuration["S3RoleArn"],
                    ManifestKey = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm") + "/index.m3u8"
                }
            };
            var result = await _mediaPackageClient.CreateHarvestJobAsync(request);
            return Ok(result);
        }
    }
}