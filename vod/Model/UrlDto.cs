using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VODFunctions.Model
{
    public class UrlDto
    {
        public string Url { get; set; }
        public DateTimeOffset? ExpiryTime { get; set; }
    }
}
