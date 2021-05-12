using Amazon.S3;
using Amazon.S3.Model;
using LazyCache;
using System;
using System.IO;
using System.Threading.Tasks;

namespace VODFunctions.Services
{
    public class KeyRepository
    {
        private readonly IAppCache _cache;
        private readonly IAmazonS3 _s3Client;
        private readonly string _s3KeyBucketName;
        private readonly string _dashKeyGroup;

        public KeyRepository(IAppCache cache, IAmazonS3 s3Client, string s3KeyBucketName, string dashKeyGroup)
        {
            _cache = cache;
            _s3Client = s3Client;
            _s3KeyBucketName = s3KeyBucketName;
            _dashKeyGroup = dashKeyGroup;
        }

        public async Task<byte[]> GetKeyAsync(string keyGroup, string keyId)
        {
            return await _cache.GetOrAddAsync($"{keyGroup}/{keyId}", async cacheEntry => {
                cacheEntry.SlidingExpiration = TimeSpan.FromSeconds(180);
                return await ReadAmazonS3Object(_s3KeyBucketName, $"{keyGroup}/{keyId}");
            });
        }

        private async Task<byte[]> ReadAmazonS3Object(string bucket, string key)
        {
            var request = new GetObjectRequest
            {
                BucketName = bucket,
                Key = key
            };

            using var response = await _s3Client.GetObjectAsync(request);
            using var ms = new MemoryStream();

            response.ResponseStream.CopyTo(ms);
            return ms.ToArray();
        }

        public async Task<byte[]> GetKeyWithDASHGroupAsync(string keyId) {
            return await this.GetKeyAsync(_dashKeyGroup, keyId);
        }
    }
}
