using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TuffermanScraper.Test.Models
{
    public class ShelvingRow
    {
        public string RangeOrHeader { get; set; } = "";

        public int? NumberOfShelves { get; set; }
        public string? Component { get; set; }
        public string? ShelfType { get; set; }
        public int? Height { get; set; }
        public int? Width { get; set; }
        public int? Depth { get; set; }
        public int? MaxLoadPerLevel { get; set; }
        public string? UprightColour { get; set; }
        public string? BeamColour { get; set; }
        public string? Finish { get; set; }
        public string? Assembly { get; set; }
        public string? Supplier { get; set; }
        public decimal? Was { get; set; }
        public decimal? Now { get; set; }
        public int? WarrantyYears { get; set; }
        public int? DeliveryDays { get; set; }
        public string? AddOnBay { get; set; }      // "Yes"/"No" or null
        public decimal? Now2 { get; set; }         // second "Now" column
        public string? Location { get; set; }
        public string? Bullets { get; set; }
        public string? Comments { get; set; }
    }
}
