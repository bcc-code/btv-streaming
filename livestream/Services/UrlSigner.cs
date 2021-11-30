using Amazon.CloudFront;
using LazyCache;
using System;
using System.IO;

namespace LivestreamFunctions.Services
{
    public class UrlSigner
    {
        private readonly IAmazonCloudFront _cfClient;
        private readonly string _privateKey;
        private readonly string _keyPairId;

        public UrlSigner(IAmazonCloudFront cfClient, string privateKey, string keyPairId)
        {
            _cfClient = cfClient;
            _privateKey = privateKey;
            _keyPairId = keyPairId;
        }

        public string Sign(string url, DateTime expiry = default)
        {
            if (expiry == default)
            {
                expiry = DateTime.Now.AddHours(6);
            }
            using var privateKeyReader = new StringReader(_privateKey);
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
            {
                throw new ApplicationException("url is not valid: " + url);
            }
            var tempUrl = AmazonCloudFrontUrlSigner.GetCustomSignedURL(
                AmazonCloudFrontUrlSigner.Protocol.https,
                uri.Host.ToLower(),
                privateKeyReader,
                "*",
                _keyPairId,
                expiry,
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
