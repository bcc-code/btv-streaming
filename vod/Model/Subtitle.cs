using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VODStreaming.Model
{
    public class Subtitle
    {
        public string Url { get; set; }
        public string Type { get; set; }
        public string Filename { get; set; }
        public string Label { get; set; }
        public string LanguageCode { get; set; }
    }
}
