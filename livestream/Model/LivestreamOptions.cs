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
        public string HlsUrlCmafV2 { get; set; }
        public string ExtraHosts { get; set; }
        public List<string> GetAllowedHosts() {
            var allowedHosts = new List<string>
            {
                new Uri(HlsUrl, UriKind.Absolute).Host,
                new Uri(HlsUrl2, UriKind.Absolute).Host,
                new Uri(HlsUrlCmafV2, UriKind.Absolute).Host
            };
            if (!string.IsNullOrWhiteSpace(ExtraHosts))
            {
                allowedHosts.AddRange(ExtraHosts.Split(','));
            }
            return allowedHosts;
        }
    }
}
