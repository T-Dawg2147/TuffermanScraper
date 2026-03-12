using System;
using System.Linq;
using TuffermanScraper.ExtractTest.Models;
using TuffermanScraper.ExtractTest.Utils;

namespace TuffermanScraper.ExtractTest.Transformers
{
    public static class ShelvingRowTransformer
    {
        public static ShelvingRow FromVariant(TuffermanVariant v, string rangeHeaderText)
        {
            var bullets = v.Bullets ?? "";
            var title = v.BaseTitle ?? "";
            var allText = (title + " " + bullets).ToLowerInvariant();

            // ----- Shelf type -----
            string? shelfType = null;
            if (allText.Contains("mdf"))
                shelfType = "Solid/MDF/Steel";
            else if (allText.Contains("chipboard"))
                shelfType = "Solid/Chipboard/Steel";
            else if (allText.Contains("wire") || allText.Contains("mesh"))
                shelfType = "Wire mesh/Steel";
            else if (allText.Contains("hdf"))
                shelfType = "Solid/HDF/Steel";
            else if (allText.Contains("melamine"))
                shelfType = "Solid/Melamine/Steel";

            // ----- Dimensions -----
            int? height = v.HeightMm;
            int? width = v.WidthMm;
            int? depth = v.DepthMm;

            if (height == null || width == null || depth == null)
            {
                var dims = DimensionsParsing.ParseDimensionsFromText(allText);

                if (height == null && dims.H != null) height = dims.H;
                if (width == null && dims.W != null) width = dims.W;
                if (depth == null && dims.D != null) depth = dims.D;
            }


            // ----- Assembly -----
            string? assembly = null;
            string self = "Self";
            string fullAssembled = "Fully Assembled";
            if (allText.Contains("boltless") || allText.Contains("bolt-free") || allText.Contains("bolt free"))
                assembly = self;
            else if (allText.Contains("self assembly") || allText.Contains("self-assembly"))
                assembly = self;
            else if (allText.Contains("fully assembled") || allText.Contains("comes assembled"))
                assembly = fullAssembled;
            else if (allText.Contains("easy assembly") && allText.Contains("no nuts") && allText.Contains("no bolts"))
                assembly = self;
            else if (allText.Contains("no nuts and bolts"))
                assembly = self;
            else if (allText.Contains("flat packed") && (allText.Contains("assembly") || allText.Contains("assemble")))
                assembly = self;

            // ----- Finish -----
            string? finish = null;
            if (allText.Contains("powder coated") || allText.Contains("powder-coated"))
                finish = "Powder Coated";
            else if (allText.Contains("epoxy"))
                finish = "Epoxy";
            else if (allText.Contains("galvanised") || allText.Contains("galvanized"))
                finish = "Galvanised";
            else if (allText.Contains("stainless steel"))
                finish = "Stainless Steel";
            else if (allText.Contains("nickel chrome") || allText.Contains("chrome shelving"))
                finish = "Nickel Chrome";

            // ----- Colours -----
            string? uprightColour = null;
            string? beamColour = null;
            if (!string.IsNullOrWhiteSpace(v.Colour))
                uprightColour = v.Colour;

            // ----- Component / Add-on bay -----
            string? component = null;
            string? addOnBay = null;
            if (allText.Contains("extension bay") || allText.Contains("add-on bay") || allText.Contains("add on bay"))
            {
                component = "Add-on bay";
                addOnBay = "Yes";
            }
            else if (allText.Contains("starter bay"))
            {
                component = "Starter bay";
                addOnBay = "No";
            }
            else
            {
                addOnBay = "No";
            }

            // ----- Warranty -----
            int? warrantyYears = null;

            if (!string.IsNullOrWhiteSpace(v.Supplier) &&
                v.Supplier.Equals("Storalex", StringComparison.OrdinalIgnoreCase))
            {
                warrantyYears = 5;
            }
            else 
            {
                if (allText.Contains("year"))
                {
                    foreach (var word in allText.Split(' ', '\n', '\r', '\t'))
                    {
                        if (int.TryParse(word.TrimEnd('+'), out var n) && n > 0 && n <= 20)
                        {
                            n = n != 0 ? n : 1;
                            warrantyYears = n;
                            break;
                        }
                    }
                }
                warrantyYears ??= 1;
            }

            // ----- Delivery -----
            int? deliveryDays = v.DeliveryDays; // already computed by scraper

            // ----- Number of Shelves -----
            var numberOfShelves = v.Levels;
            if (numberOfShelves == null)
                numberOfShelves = Parsing.ParseShelvesFromText(allText);

            var row = new ShelvingRow
            {
                RangeOrHeader = rangeHeaderText,
                NumberOfShelves = numberOfShelves,
                Component = component,
                ShelfType = shelfType,
                Height = height,
                Width = width,
                Depth = depth,
                MaxLoadPerLevel = v.LoadPerShelfKg,
                UprightColour = uprightColour,
                BeamColour = beamColour,
                Finish = finish,
                Assembly = assembly,
                Supplier = string.IsNullOrWhiteSpace(v.Supplier) ? null : v.Supplier,
                Was = v.WasPriceExVat,
                Now = v.NowPriceExVat,
                WarrantyYears = warrantyYears,
                DeliveryDays = deliveryDays,
                AddOnBay = addOnBay,
                Now2 = null,
                Location = v.Url,
                Bullets = v.Bullets,
                Comments = null
            };

            return row;
        }
    }
}