using LivestreamFunctions.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;

namespace LivestreamFunctions.Tests
{
    public class CmafTests
    {
        private CmafProxyService _proxy;
        private readonly Dictionary<string, string> _manifests = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _expected = new Dictionary<string, string>();

        [OneTimeSetUp]
        public void CommonSetUp()
        {
            var logger = new Mock<ILogger<CmafProxyService>>();
            var httpClientFactory = new Mock<IHttpClientFactory>();
            var memoryCache = new Mock<IMemoryCache>();
            _proxy = new CmafProxyService(logger.Object, httpClientFactory.Object, memoryCache.Object);

            var expectedFolder = "./manifests/cmaf/expected";
            var sourceFolder = "./manifests/cmaf/source";

            foreach (var path in Directory.GetFiles(expectedFolder))
            {
                var text = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(text))
                {
                    throw new System.Exception("A test file is empty: " + expectedFolder + "/" + path);
                }
                var filename = Path.GetFileName(path);
                _expected.Add(filename, text);
            }

            foreach (var path in Directory.GetFiles(sourceFolder))
            {
                var text = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(text))
                {
                    throw new System.Exception("A test file is empty: " + sourceFolder + "/" + path);
                }
                var filename = Path.GetFileName(path);
                _manifests.Add(filename, text);
            }
        }

        [Test]
        public void ProxyTopLevel()
        {
            var manifest = _manifests["master.m3u8"];
            var expected = _expected["master.m3u8"];
            var result = _proxy.CustomizeTopLevelManifest(manifest, "https://cdn.example.com/livestream/master_playlist.m3u8?Policy=eyJzb21ldGhpbmciOiJzb21ldGhpbmdzb21ldGhpbmdzb21ldGhpbmdzb21ldGhpbmdzb21ldGhpbmdzb21ldGhpbmdzb21ldGhpbmdzb21ldGhpbmcifQ&Signature=_Sx~D6IKsm-qfeTY_xuNeL56f~mr-F-i~_&Key-Pair-ID=ABCD12345", "https://secondlevel.example.com/api/cmaf-proxy");
            Assert.AreEqual(expected, result);
        }

        [Test]
        public void ProxyVideo()
        {
            var manifest = _manifests["video.m3u8"];
            var expected = _expected["video.m3u8"];
            var result = _proxy.CustomizeSecondLevelManifestAsync(manifest, "https://cdn.example.com/livestream/video_1.m3u8?Policy=eyJzb21ldGhpbmciOiJzb21ldGhpbmdzb21ldGhpbmdzb21ldGhpbmdzb21ldGhpbmdzb21ldGhpbmdzb21ldGhpbmdzb21ldGhpbmdzb21ldGhpbmcifQ&Signature=_Sx~D6IKsm-qfeTY_xuNeL56f~mr-F-i~_&Key-Pair-ID=ABCD12345");
            Assert.AreEqual(expected, result);
        }

        [Test]
        public void ProxyAudio()
        {
            var manifest = _manifests["audio.m3u8"];
            var expected = _expected["audio.m3u8"];
            var result = _proxy.CustomizeSecondLevelManifestAsync(manifest, "https://cdn.example.com/livestream/audio_1_2.m3u8?Policy=eyJzb21ldGhpbmciOiJzb21ldGhpbmdzb21ldGhpbmdzb21ldGhpbmdzb21ldGhpbmdzb21ldGhpbmdzb21ldGhpbmdzb21ldGhpbmdzb21ldGhpbmcifQ&Signature=_Sx~D6IKsm-qfeTY_xuNeL56f~mr-F-i~_&Key-Pair-ID=ABCD12345");
            Assert.AreEqual(expected, result);
        }

        [Test]
        public void ProxyIFrame()
        {
            var manifest = _manifests["iframe.m3u8"];
            var expected = _expected["iframe.m3u8"];
            var result = _proxy.CustomizeSecondLevelManifestAsync(manifest, "https://cdn.example.com/livestream/iframe_1_2_3.m3u8?Policy=eyJzb21ldGhpbmciOiJzb21ldGhpbmdzb21ldGhpbmdzb21ldGhpbmdzb21ldGhpbmdzb21ldGhpbmdzb21ldGhpbmdzb21ldGhpbmdzb21ldGhpbmcifQ&Signature=_Sx~D6IKsm-qfeTY_xuNeL56f~mr-F-i~_&Key-Pair-ID=ABCD12345");
            Assert.AreEqual(expected, result);
        }

        [Test]
        public void ProxySubtitles()
        {
            var manifest = _manifests["subtitle.m3u8"];
            var expected = _expected["subtitle.m3u8"];
            var result = _proxy.CustomizeSecondLevelManifestAsync(manifest, "https://cdn.example.com/livestream/subtitles.m3u8?Policy=eyJzb21ldGhpbmciOiJzb21ldGhpbmdzb21ldGhpbmdzb21ldGhpbmdzb21ldGhpbmdzb21ldGhpbmdzb21ldGhpbmdzb21ldGhpbmdzb21ldGhpbmcifQ&Signature=_Sx~D6IKsm-qfeTY_xuNeL56f~mr-F-i~_&Key-Pair-ID=ABCD12345");
            Assert.AreEqual(expected, result);
        }
    }
}
