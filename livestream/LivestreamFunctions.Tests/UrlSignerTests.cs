using Amazon;
using Amazon.CloudFront;
using LazyCache;
using LivestreamFunctions.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;

namespace LivestreamFunctions.Tests
{
    public class UrlSignerTests
    {
        private UrlSigner _urlSigner;

        [OneTimeSetUp]
        public void CommonSetUp()
        {
            var privateKey = Encoding.UTF8.GetString(Convert.FromBase64String("LS0tLS1CRUdJTiBSU0EgUFJJVkFURSBLRVktLS0tLQpNSUlDWEFJQkFBS0JnUUNxR0t1a08xRGU3emhaajYrSDBxdGpUa1Z4d1RDcHZLZTRlQ1owRlBxcmkwY2IySlpmWEovRGdZU0Y2dlVwCndtSkc4d1ZRWktqZUdjakRPTDVVbHN1dXNGbmNDeldCUTdSS05VU2VzbVFSTVNHa1ZiMS8zaitza1o2VXRXKzV1MDlsSE5zajZ0UTUKMXMxU1ByQ0JrZWRiTmYwVHAwR2JNSkR5UjRlOVQwNFpad0lEQVFBQkFvR0FGaWprbzU2K3FHeU44TTBSVnlhUkFYeisreFRxSEJMaAozdHg0VmdNdHJRK1dFZ0NqaG9Ud28yM0tNQkF1SkdTWW5SbW9CWk0zbE1mVEtldklrQWlkUEV4dllDZG01ZFlxM1hUb0xra0x2NUwyCnBJSVZPRk1ERytLRVNuQUZWN2wyYytjbnpSTVcwK2I2ZjhtUjFDSnpadXhWTEw2UTAyZnZMaTU1L21iU1l4RUNRUURlQXc2ZmlJUVgKR3VrQkk0ZU1aWnQ0bnNjeTJvMTJLeVluZXIzVnBvZUUrTnAycStaM3B2QU1kL2FOelEvVzlXYUkrTlJmY3hVSnJtZlB3SUdtNjNpbApBa0VBeENMNUhRYjJiUXI0QnlvcmNNV20vaEVQMk1aelJPVjczeUY0MWhQc1JDOW02NktyaGVPOUhQVEp1bzMvOXM1cCtzcUd4T2xGCkwwTkR0NFNrb3NqZ0d3SkFGa2x5UjF1Wi93UEpqajYxMWNkQmN6dGxQZHFveHNzUUduaDg1QnpDai91M1dxQnBFMnZqdnl5dnlJNWsKWDZ6azdTMGxqS3R0MmpueTIrMDBWc0JlclFKQkFKR0MxTWc1T3lkbzVOd0Q2QmlST3JQeEdvMmJwVGJ1L2ZoclQ4ZWJIa1R6MmVwbApVOVZRUVNRelkxb1pNVlg4aTFtNVdVVExQejJ5TEpJQlFWZFhxaE1DUUJHb2l1U29TamFmVWhWN2kxY0VHcGI4OGg1TkJZWnpXWEdaCjM3c0o1UXNXK3NKeW9OZGUzeEg4dmRYaHpVN2VUODJENlgvc2N3OVJaeisvNnJDSjRwMD0KLS0tLS1FTkQgUlNBIFBSSVZBVEUgS0VZLS0tLS0="));
            var cf = new AmazonCloudFrontClient(RegionEndpoint.EUNorth1);
            _urlSigner = new UrlSigner(cf, privateKey, "ABCD3FGH1JKLM");
        }

        [Test]
        public void UrlAsExpected()
        {
            var expectedPolicy = "eyJTdGF0ZW1lbnQiOiBbeyJSZXNvdXJjZSI6Imh0dHBzOi8vY2RuLmV4YW1wbGUuY29tLyoiLCJDb25kaXRpb24iOnsiRGF0ZUxlc3NUaGFuIjp7IkFXUzpFcG9jaFRpbWUiOjk1MTAwNDgwMH19fV19";
            var expectedSignature = "pX5zEjFHIcjNsqNKjvc9qYMl35uvHFZ~TqQTSIg~2W4DexUfeCYHT5a3muWdBOacyOKF0uH7ZG8sqxTi4tNlOF4lKAhoNckdfvzz0-0yDg79GWD5iQYTT6dXRv0Ra-6e-rC0WEsoYcq02onOS6IpMkMc2CgGEkc5m3yDOsmkM~Q_";
            var expectedKeyPairId = "ABCD3FGH1JKLM";
            var expected = "https://cdn.example.com/livestream/master_playlist.m3u8"
                + "?Policy=" + expectedPolicy
                + "&Signature=" + expectedSignature
                + "&Key-Pair-Id=" + expectedKeyPairId;
            var dateTime = DateTime.ParseExact("20000220", "yyyyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal);
            TestContext.WriteLine(dateTime);
            var result = _urlSigner.Sign("https://cdn.example.com/livestream/master_playlist.m3u8", dateTime);
            TestContext.WriteLine(result);
            Assert.AreEqual(expected, result);
        }

    }
}
