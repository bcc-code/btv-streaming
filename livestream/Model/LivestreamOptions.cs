using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LivestreamFunctions.Model
{
    public class LivestreamOptions
    {
        public const string ConfigurationSection = "Live";
        public string HlsUrl { get; set; }
    }
}
