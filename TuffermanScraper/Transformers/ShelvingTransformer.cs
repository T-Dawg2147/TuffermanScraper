using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TuffermanScraper.Models;

namespace TuffermanScraper.Transformers
{
    public static class ShelvingTransformer
    {
        public static class ShelvingRowTransformer
        {
            public static ShelvingRow FromVariant(
                TuffermanVariant v,
                string rangeHeaderText) // e.g. "Light Duty - Up to 340kg UDL"
            {
                var bullets = v.Bullets ?? "";
                var title = v.BaseTitle ?? "";
                var allText = (title + " " + bullets).ToLowerInvariant();

                string? shelfType = null;
                if (allText.Contains("chipboard"))
                    shelfType = "Solid/ Chipboard/ Steel ";
                else if (allText.Contains("mdf"))
                    shelfType = "Solid/ MDF / Steel";
                else if (allText.Contains("wire") || allText.Contains("mesh"))
                    shelfType = "Wire mesh/ steel";
                else if (allText.Contains("hdf"))
                    shelfType = "Solid/ HDF/Steel";

                string? assembly = null;
                if (allText.Contains("boltless"))
                    assembly = "Self";
                else if (allText.Contains("fully assembled"))
                    assembly = "Fully assembled";

                string? finish = null;
                if (allText.Contains("powder coated") || allText.Contains("epoxy"))
                    finish = "Epoxy ";

                string? uprightColour = null;
                string? beamColour = null;

                // Very conservative colour parsing:
                if (allText.Contains("blue") || allText.Contains("grey") || allText.Contains("gray") ||
                    allText.Contains("black") || allText.Contains("orange") || allText.Contains("white"))
                {
                    // We'll just record the single scraped colour for uprights;
                    // if later you want more nuance, we can add rules here.
                    if (!string.IsNullOrWhiteSpace(v.Colour))
                        uprightColour = v.Colour;
                }

                string? component = null;
                string? addOnBay = null;
                if (allText.Contains("add-on") || allText.Contains("add on") || allText.Contains("extension bay"))
                {
                    component = "Add-on bay";
                    addOnBay = "Yes";
                }
                else if (allText.Contains("starter bay"))
                {
                    component = "Starter bay";
                    addOnBay = "No";
                }

                int? warrantyYears = null;
                if (allText.Contains("year"))
                {
                    foreach (var word in allText.Split(' ', '\n', '\r', '\t'))
                    {
                        if (int.TryParse(word.TrimEnd('+'), out var n) && n > 0 && n <= 20)
                        {
                            warrantyYears = n;
                            break;
                        }
                    }
                }

                int? deliveryDays = null;
                if (allText.Contains("next day") || allText.Contains("24hr") || allText.Contains("24 hr"))
                    deliveryDays = 1;
                else if (allText.Contains("3-5 day") || allText.Contains("3 to 5 day"))
                    deliveryDays = 5; 

                var row = new ShelvingRow
                {
                    Range = rangeHeaderText,
                    NumberOfShelves = v.Levels,
                    Component = component,
                    ShelfType = shelfType,
                    Height = v.HeightMm,
                    Width = v.WidthMm,
                    Depth = v.DepthMm,
                    MaxLoadPerLevel = v.LoadPerShelfKg,
                    UprightColour = uprightColour,
                    BeamColour = beamColour,
                    Finish = finish,
                    Assembly = assembly,
                    Supplier = "Tufferman",         //
                    Was = v.WasPriceExVat,
                    Now = v.NowPriceExVat,
                    WarrentyYears = warrantyYears,
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
}
