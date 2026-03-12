using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TuffermanScraper.Test.Models
{
    public sealed class TuffermanVariantRow
    {
        public string Range { get; set; } = string.Empty;
        public int NumberOfShelves { get; set; }
        public string Component { get; set; } = string.Empty;
        public string ShelfType { get; set; } = string.Empty;
        public int? HeightMm { get; set; }
        public int? WidthMm { get; set; }
        public int? DepthMm { get; set; }
        public int? MaxLoadPerLevelKg { get; set; }
        public string UprightColour { get; set; } = string.Empty;
        public string BeamColour { get; set; } = string.Empty;
        public string Finish { get; set; } = string.Empty;
        public string Assembly { get; set; } = string.Empty;
        public string Supplier { get; set; } = string.Empty;
        public decimal? WasPriceExVat { get; set; }
        public decimal? NowPriceExVat { get; set; }
        public int? WarrantyYears { get; set; }
        public int? DeliveryDays { get; set; }
        public bool IsAddOnBay { get; set; }
        public string LocationUrl { get; set; } = string.Empty;
        public string Bullets { get; set; } = string.Empty;
        public string Comments { get; set; } = string.Empty;
    }
}
