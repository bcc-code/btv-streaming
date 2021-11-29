using Amazon.CloudFront;
using Amazon.S3;
using Amazon.S3.Model;
using LazyCache;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace LivestreamFunctions.Services
{
    public class UrlSigner
    {
        private readonly IAppCache _cache;
        private readonly IAmazonCloudFront _cfClient;
        private readonly string _privateKey;
        private readonly string _keyPairId;
        private readonly string _s3KeyBucketName;
        private readonly string _dashKeyGroup;

        public UrlSigner(IAppCache cache, IAmazonCloudFront cfClient, string privateKey, string keyPairId)
        {
            _cache = cache;
            _cfClient = cfClient;
            _privateKey = privateKey;
            _keyPairId = keyPairId;
        }

        public class UrlSigningConfig
        {
            public string PolicyTemplate { get; set; }
            public string XmlPrivateKey { get; set; }
            public string PrivateKeyId { get; set; }
            public string Url { get; set; }
            public TimeSpan ExpiresAfter { get; set; }
        }

        public string Sign(string url)
        {
            using var privateKeyReader = new StringReader(_privateKey);
            Uri host;
            if (!Uri.TryCreate(url, UriKind.Absolute, out host))
            {
                throw new ApplicationException("url is not valid: " + url);
            }
            var tempUrl = AmazonCloudFrontUrlSigner.GetCustomSignedURL(
                AmazonCloudFrontUrlSigner.Protocol.https,
                host.ToString().ToLower(),
                privateKeyReader,
                "*",
                _keyPairId,
                DateTime.Now.AddHours(6),
                null);
            
            var queryParams = tempUrl[(tempUrl.IndexOf("?") + 1)..];
            var querySeperator = "?";
            if (url.Contains("?"))
            {
                querySeperator = "&";
            }

            return url + querySeperator + queryParams;
        }
    }
}
