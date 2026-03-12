using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TuffermanScraper.Test.Models
{
    public class TuffermanVariant
    {
        public string BaseTitle { get; set; } = "";
        public string RangeOrCategory { get; set; } = "";
        public int? HeightMm { get; set; }
        public int? WidthMm { get; set; }
        public int? DepthMm { get; set; }
        public int? LoadPerShelfKg { get; set; }
        public int? Levels { get; set; }
        public int? Units { get; set; }
        public string Colour { get; set; } = "";
        public decimal? WasPriceExVat { get; set; }
        public decimal? NowPriceExVat { get; set; }
        public int? DeliveryDays { get; set; }
        public string Url { get; set; } = "";
        public string Bullets { get; set; } = "";
        public string Supplier { get; set; } = "";
    }
}
