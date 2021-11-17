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

        public string GetUrl(string url)
        {
            return FetchAndSignUrlAsync(url);
        }

        private string FetchAndSignUrlAsync(string url)
        {
            using var sr = new StringReader(_privateKey);
            var host = new Uri(url).Host.ToLower();
            var tempUrl = AmazonCloudFrontUrlSigner.GetCustomSignedURL(AmazonCloudFrontUrlSigner.Protocol.https, host, sr, "*", _keyPairId, DateTime.Now.AddHours(6), null);

            var queryParams = tempUrl[tempUrl.IndexOf("?")..];

            return url + queryParams;
        }

        public static string ToUrlSafeBase64String(byte[] bytes)
        {
            return System.Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('=', '_')
                .Replace('/', '~');
        }

        public static string CreateCannedPrivateURL(UrlSigningConfig config)
        {
            // args[] 0-thisMethod, 1-resourceUrl, 2-seconds-minutes-hours-days 
            // to expiration, 3-numberOfPreviousUnits, 4-pathToPolicyStmnt, 
            // 5-pathToPrivateKey, 6-PrivateKeyId

            // Create the policy statement.
            string policyStatement = CreatePolicyStatement(config.PolicyTemplate,
                config.Url,
                DateTime.Now,
                DateTime.Now.Add(config.ExpiresAfter),
                "0.0.0.0/0");
            if ("Error!" == policyStatement) return "Invalid time frame." +
                "Start time cannot be greater than end time.";

            // Copy the expiration time defined by policy statement.
            var strExpiration = CopyExpirationTimeFromPolicy(policyStatement);

            // Read the policy into a byte buffer.
            var bufferPolicy = Encoding.ASCII.GetBytes(policyStatement);

            // Initialize the SHA1CryptoServiceProvider object and hash the policy data.
            using var cryptoSHA1 = new SHA1CryptoServiceProvider();
            bufferPolicy = cryptoSHA1.ComputeHash(bufferPolicy);

            // Initialize the RSACryptoServiceProvider object.
            var providerRSA = new RSACryptoServiceProvider();

            // Format the RSACryptoServiceProvider providerRSA and 
            // create the signature.
            providerRSA.FromXmlString(config.XmlPrivateKey);
            var rsaFormatter = new RSAPKCS1SignatureFormatter(providerRSA);
            rsaFormatter.SetHashAlgorithm("SHA1");
            var signedPolicyHash = rsaFormatter.CreateSignature(bufferPolicy);

            // Convert the signed policy to URL-safe base64 encoding and 
            // replace unsafe characters + = / with the safe characters - _ ~
            var strSignedPolicy = ToUrlSafeBase64String(signedPolicyHash);

            // Concatenate the URL, the timestamp, the signature, 
            // and the key pair ID to form the signed URL.
            return config.Url +
                "?Expires=" + strExpiration +
                "&Signature=" + strSignedPolicy +
                "&Key-Pair-Id=" + config.PrivateKeyId;
        }

        public static string CreatePolicyStatement(string policyStmnt,
           string resourceUrl,
           DateTime startTime,
           DateTime endTime,
           string ipAddress)
        {
            var startTimeSpanFromNow = (startTime - DateTime.Now);
            var endTimeSpanFromNow = (endTime - DateTime.Now);
            var intervalStart =
               (DateTime.UtcNow.Add(startTimeSpanFromNow)) -
               new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var intervalEnd =
               (DateTime.UtcNow.Add(endTimeSpanFromNow)) -
               new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var startTimestamp = (int)intervalStart.TotalSeconds; // START_TIME
            var endTimestamp = (int)intervalEnd.TotalSeconds;  // END_TIME

            if (startTimestamp > endTimestamp)
                return "Error!";

            // Replace variables in the policy statement.
            policyStmnt = policyStmnt.Replace("RESOURCE", resourceUrl);
            policyStmnt = policyStmnt.Replace("START_TIME", startTimestamp.ToString());
            policyStmnt = policyStmnt.Replace("END_TIME", endTimestamp.ToString());
            policyStmnt = policyStmnt.Replace("IP_ADDRESS", ipAddress);
            policyStmnt = policyStmnt.Replace("EXPIRES", endTimestamp.ToString());
            return policyStmnt;
        }

        public static string CopyExpirationTimeFromPolicy(string policyStatement)
        {
            var startExpiration = policyStatement.IndexOf("EpochTime");
            var strExpirationRough = policyStatement.Substring(startExpiration + "EpochTime".Length);
            char[] digits = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };

            var listDigits = new List<char>(digits);
            var buildExpiration = new StringBuilder(20);

            foreach (var c in strExpirationRough)
            {
                if (listDigits.Contains(c))
                {
                    buildExpiration.Append(c);
                }
            }
            return buildExpiration.ToString();
        }
    }
}
