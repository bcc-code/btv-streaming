using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LivestreamFunctions.Model
{
    public class LivestreamOptions
    {
        public string HlsUrl { get; set; }
        public string HlsUrl2 { get; set; }
        public string ExtraHosts { get; set; }
        public string GetAllowedHosts() {
            var hlsUrl = new Uri(HlsUrl, UriKind.Absolute);
            var hlsUrl2 = new Uri(HlsUrl2, UriKind.Absolute);
            var allowedHosts = hlsUrl.Host + "," + hlsUrl2.Host;
            if (!string.IsNullOrWhiteSpace(ExtraHosts))
            {
                allowedHosts += "," + ExtraHosts;
            }
            return allowedHosts.ToLowerInvariant();
        }
    }
}
