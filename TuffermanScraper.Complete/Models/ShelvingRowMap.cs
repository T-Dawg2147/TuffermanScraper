using CsvHelper.Configuration;

namespace TuffermanScraper.Complete.Models
{
    public sealed class ShelvingRowMap : ClassMap<ShelvingRow>
    {
        public ShelvingRowMap()
        {
            Map(m => m.RangeOrHeader).Name("Range");
            Map(m => m.NumberOfShelves).Name("Number of shelves");
            Map(m => m.Component).Name("Component");
            Map(m => m.ShelfType).Name("Shelf type");
            Map(m => m.Height).Name("Height ");
            Map(m => m.Width).Name("Width");
            Map(m => m.Depth).Name("Depth");
            Map(m => m.MaxLoadPerLevel).Name("Max load per level");
            Map(m => m.UprightColour).Name("Upright colour");
            Map(m => m.BeamColour).Name("Beam colour ");
            Map(m => m.Finish).Name("Finish");
            Map(m => m.Assembly).Name("Assembly");
            Map(m => m.Supplier).Name("Supplier");
            Map(m => m.Was).Name("Was");
            Map(m => m.Now).Name("Now");
            Map(m => m.WarrantyYears).Name("Warranty (Years)");
            Map(m => m.DeliveryDays).Name("Delivery (Days) ");
            Map(m => m.AddOnBay).Name("Add on bay?");
            Map(m => m.Now2).Name("Now");
            Map(m => m.Location).Name("Location");
            Map(m => m.Bullets).Name("Bullets");
            Map(m => m.Comments).Name("Comments");
        }
    }
}
