using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TuffermanScraper.Models
{
    public sealed class TuffermanVariant
    {
        /* These are the fields i would suggest
        public string BaseTitle { get; set; } = string.Empty;
        public string CategoryOrRange { get; set; } = string.Empty;
        public int? HeightMm { get; set; }
        public int? WidthMm { get; set; }
        public int? DepthMm { get; set; }
        public int? LoadPerShelfKg { get; set; }
        public int? Levels { get; set; }
        public int? Units { get; set; }
        public string? UprightColor { get; set; } = string.Empty;
        public string? BeamColour { get; set; } = string.Empty;
        public string? Finish { get; set; } = string.Empty;
        public string? Supplier { get; set; } = string.Empty;
        public double? WasPriceExVat { get; set; }
        public double? NowPriceExVat { get; set; }

        public string Url { get; set; } = string.Empty;
        public string Bullets { get; set; } = string.Empty;
        */

        public string BaseTitle { get; set; } = "";
        public string RangeOrCategory { get; set; } = "";   // e.g. "VRS Heavy Duty Shelving"
        public int? HeightMm { get; set; }
        public int? WidthMm { get; set; }
        public int? DepthMm { get; set; }
        public int? LoadPerShelfKg { get; set; }
        public int? Levels { get; set; }
        public int? Units { get; set; }
        public string Colour { get; set; } = "";
        public decimal? WasPriceExVat { get; set; }
        public decimal? NowPriceExVat { get; set; }
        public string Url { get; set; } = "";
        public string Bullets { get; set; } = "";
        public string Supplier { get; set; } = "Tufferman";
    }
}
