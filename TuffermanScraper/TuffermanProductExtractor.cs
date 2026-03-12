using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using TuffermanScraper.Test.Models;

namespace TuffermanScraper.Test
{
    public static class TuffermanProductExtractor
    {
        // Product Manager ranges
        private static readonly string[] AllRanges =
        {
            "Light Duty Shelving", "Medium Duty Shelving", "Heavy Duty Shelving",
            "Archive Shelving", "Tubular Shelving", "Mobile Shelving",
            "Longspan Shelving", "Catering Shelving", "Cantilever Racking",
            "Type Shelving", "Wall Mounted Shelving", "Retail Shelving",
            "Office Shelving", "Shlving with Boxes", "Warehouse Shelving",
            "Specialist Shelving", "Plastic Shelving", "Metal Shelving",
            "Industrial Shelving", "Boltless Shelving", "Wire Shelving"
        };

        /// <summary>
        /// Main entry: takes the live HTML you already downloaded and the product URL,
        /// and returns one row per variant.
        /// </summary>
        public static List<TuffermanVariantRow> ExtractFromHtml(string html, string productUrl)
        {
            var rows = new List<TuffermanVariantRow>();

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var productJson = ExtractProductJson(doc);
            if (productJson?.Product == null)
                return rows;

            var product = productJson.Product;

            // Flatten the full Shopify description HTML into a single text string.
            // This already includes Key Features, Ideal Uses, Product Overview, tables, etc.
            var descriptionText = ExtractDescriptionText(product.DescriptionHtml ?? string.Empty);

            var range = DetectRange(product.Title ?? "", descriptionText);
            var supplier = product.Vendor ?? "";

            var warrantyYears = DetectWarrantyYears(descriptionText);
            var deliveryDays = DetectDeliveryDays(html);

            var component = DetectComponent(descriptionText);
            var shelfType = DetectShelfType(descriptionText);
            var finish = DetectFinish(descriptionText);
            var assembly = DetectAssembly(descriptionText);

            var numberOfShelves = DetectNumberOfShelves(descriptionText) ?? 0;

            var height = DetectHeightFromTitle(product.Title) ?? DetectHeightFromUrl(productUrl);

            foreach (var v in product.Variants)
            {
                var row = new TuffermanVariantRow
                {
                    Range = range,
                    NumberOfShelves = numberOfShelves,
                    Component = component,
                    ShelfType = shelfType,
                    HeightMm = height,
                    WidthMm = SafeParseInt(v.Option1),
                    DepthMm = SafeParseInt(v.Option2),
                    MaxLoadPerLevelKg = ParseLoad(v.Option3),

                    UprightColour = DetectColourFromTitleOrDescription(product.Title, descriptionText),
                    BeamColour = "", // leave blank unless you have a solid rule
                    Finish = finish,
                    Assembly = assembly,
                    Supplier = supplier,

                    // EX-VAT prices in pence from the JSON:
                    WasPriceExVat = ToMoney(v.CompareAtPricePence),
                    NowPriceExVat = ToMoney(v.PricePence),

                    WarrantyYears = warrantyYears,
                    DeliveryDays = deliveryDays,
                    IsAddOnBay = DetectIsAddOnBay(product.Title, descriptionText),
                    LocationUrl = productUrl,

                    // Full description text (one long pipe-separated string).
                    Bullets = descriptionText,
                    Comments = ""
                };

                rows.Add(row);
            }

            return rows;
        }

        #region JSON + description helpers

        private static ShopifyProductJsonRoot? ExtractProductJson(HtmlDocument doc)
        {
            // This script tag exists in the live pages you've pasted:
            // <script type="application/json" data-product-json> { ... } </script>
            var scriptNode = doc.DocumentNode
                .SelectSingleNode("//script[@type='application/json' and @data-product-json]");

            if (scriptNode == null)
                return null;

            var json = scriptNode.InnerText;
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<ShopifyProductJsonRoot>(json, options);
        }

        private static string ExtractDescriptionText(string productDescriptionHtml)
        {
            if (string.IsNullOrWhiteSpace(productDescriptionHtml))
                return string.Empty;

            var doc = new HtmlDocument();
            doc.LoadHtml(productDescriptionHtml);

            var sb = new StringBuilder();

            foreach (var node in doc.DocumentNode
                                    .Descendants()
                                    .Where(n => n.NodeType == HtmlNodeType.Text && !n.HasChildNodes))
            {
                var text = HtmlEntity.DeEntitize(node.InnerText)
                                        .Replace("\r", " ")
                                        .Replace("\n", " ")
                                        .Trim();

                if (!string.IsNullOrEmpty(text))
                {
                    if (sb.Length > 0)
                        sb.Append(" | ");
                    sb.Append(text);
                }
            }

            return sb.ToString();
        }

        #endregion

        #region field detection

        private static string DetectRange(string? title, string description)
        {
            var haystack = ((title ?? "") + " " + (description ?? ""))
                .ToLowerInvariant();

            foreach (var range in AllRanges)
            {
                var norm = range.ToLowerInvariant();
                if (haystack.Contains(norm))
                    return range;
            }

            // Fallbacks
            if (haystack.Contains("chrome wire shelving") ||
                haystack.Contains("wire shelving"))
                return "Wire Shelving";

            if (haystack.Contains("industrial shelving"))
                return "Industrial shelving";

            if (haystack.Contains("heavy duty shelving"))
                return "Heavy Duty Shelving";

            if (haystack.Contains("light duty"))
                return "Light Duty Shelving";

            return "n/a";
        }

        private static int? DetectHeightFromTitle(string? title)
        {
            if (string.IsNullOrEmpty(title))
                return null;

            // Look for tokens like "1800mm" or "1800mm High"
            var parts = title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (part.EndsWith("mm", StringComparison.OrdinalIgnoreCase))
                {
                    var numPart = part[..^2];
                    if (int.TryParse(numPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mm))
                        return mm;
                }
            }

            return null;
        }

        private static int? DetectHeightFromUrl(string url)
        {
            var lower = url.ToLowerInvariant();
            var idx = lower.IndexOf("mm-high", StringComparison.Ordinal);
            if (idx <= 0)
                return null;

            int start = idx - 1;
            while (start >= 0 && char.IsDigit(lower[start]))
                start--;

            var num = lower.Substring(start + 1, idx - (start + 1));
            return int.TryParse(num, out var mm) ? mm : null;
        }

        private static int? ParseLoad(string? option3)
        {
            if (string.IsNullOrWhiteSpace(option3))
                return null;

            var digits = new string(option3.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out var value) ? value : null;
        }

        private static decimal? ToMoney(int? pence)
        {
            if (pence == null || pence <= 0)
                return null;
            return pence.Value / 100m;
        }

        private static int? SafeParseInt(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return null;
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
                ? v
                : null;
        }

        private static int? DetectWarrantyYears(string description)
        {
            for (int years = 1; years <= 10; years++)
            {
                var token = years.ToString(CultureInfo.InvariantCulture);
                if (description.Contains(token + " year", StringComparison.OrdinalIgnoreCase) ||
                    description.Contains(token + "-year", StringComparison.OrdinalIgnoreCase) ||
                    description.Contains(token + " years", StringComparison.OrdinalIgnoreCase))
                {
                    return years;
                }
            }

            return null;
        }

        private static int? DetectDeliveryDays(string fullHtml)
        {
            if (fullHtml.Contains("24hr DELIVERY", StringComparison.OrdinalIgnoreCase) ||
                fullHtml.Contains("next day delivery", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (fullHtml.Contains("within 4 working days", StringComparison.OrdinalIgnoreCase))
                return 4;

            return null;
        }

        private static bool DetectIsAddOnBay(string? title, string description)
        {
            var haystack = ((title ?? "") + " " + description).ToLowerInvariant();
            return haystack.Contains("extension bay") ||
                    haystack.Contains("add-on bay") ||
                    haystack.Contains("add on bay");
        }

        private static int? DetectNumberOfShelves(string description)
        {
            for (int levels = 1; levels <= 10; levels++)
            {
                var levelsToken = levels + " adjustable level";
                if (description.Contains(levelsToken, StringComparison.OrdinalIgnoreCase))
                    return levels;

                var shelvesToken = levels + " strong";
                if (description.Contains(shelvesToken, StringComparison.OrdinalIgnoreCase))
                    return levels;
            }

            return null;
        }

        private static string DetectComponent(string description)
        {
            if (description.Contains("MDF boards", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("MDF shelves", StringComparison.OrdinalIgnoreCase))
                return "Solid/MDF/Steel";

            if (description.Contains("melamine shelves", StringComparison.OrdinalIgnoreCase))
                return "Solid/Melamine/Steel";

            if (description.Contains("chrome wire shelving", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("wire shelving", StringComparison.OrdinalIgnoreCase))
                return "Wire mesh/Steel";

            return "";
        }

        private static string DetectShelfType(string description)
        {
            if (description.Contains("MDF", StringComparison.OrdinalIgnoreCase))
                return "Solid/MDF/Steel";

            if (description.Contains("melamine shelves", StringComparison.OrdinalIgnoreCase))
                return "Solid/Melamine/Steel";

            if (description.Contains("chrome wire shelving", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("wire shelving", StringComparison.OrdinalIgnoreCase))
                return "Wire mesh/Steel";

            return "";
        }

        private static string DetectFinish(string description)
        {
            if (description.Contains("powder coated", StringComparison.OrdinalIgnoreCase))
                return "Powder Coated";

            if (description.Contains("epoxy", StringComparison.OrdinalIgnoreCase))
                return "Epoxy";

            if (description.Contains("nickel chrome", StringComparison.OrdinalIgnoreCase))
                return "Nickel Chrome";

            return "";
        }

        private static string DetectAssembly(string description)
        {
            if (description.Contains("boltless", StringComparison.OrdinalIgnoreCase))
                return "Self";

            if (description.Contains("fully assembled", StringComparison.OrdinalIgnoreCase))
                return "Fully assembled";

            return "";
        }

        private static string DetectColourFromTitleOrDescription(string? title, string description)
        {
            var haystack = ((title ?? "") + " " + description).ToLowerInvariant();

            if (haystack.Contains("grey") || haystack.Contains("gray"))
                return "Grey";
            if (haystack.Contains("blue"))
                return "Blue";
            if (haystack.Contains("black"))
                return "Black";
            if (haystack.Contains("white"))
                return "White";

            return "";
        }

        #endregion

        #region CSV export

        public static void WriteCsv(string path, IEnumerable<TuffermanVariantRow> rows)
        {
            using var writer = new StreamWriter(path, false, Encoding.UTF8);

            writer.WriteLine("Range,Number of shelves,Component,Shelf type,Height ,Width,Depth,Max load per level,Upright colour,Beam colour ,Finish,Assembly,Supplier,Was,Now,Warranty (Years),Delivery (Days) ,Add on bay?,Now,Location,Bullets,Comments");

            foreach (var r in rows)
            {
                string C(object? value)
                {
                    if (value == null) return "";
                    var s = value switch
                    {
                        bool b => b ? "Yes" : "No",
                        decimal d => d.ToString("0.##", CultureInfo.InvariantCulture),
                        _ => value.ToString() ?? ""
                    };

                    if (s.Contains('"') || s.Contains(',') || s.Contains('\n'))
                    {
                        s = "\"" + s.Replace("\"", "\"\"") + "\"";
                    }
                    return s;
                }

                writer.WriteLine(string.Join(",",
                    C(r.Range),
                    C(r.NumberOfShelves),
                    C(r.Component),
                    C(r.ShelfType),
                    C(r.HeightMm),
                    C(r.WidthMm),
                    C(r.DepthMm),
                    C(r.MaxLoadPerLevelKg),
                    C(r.UprightColour),
                    C(r.BeamColour),
                    C(r.Finish),
                    C(r.Assembly),
                    C(r.Supplier),
                    C(r.WasPriceExVat),
                    C(r.NowPriceExVat),
                    C(r.WarrantyYears),
                    C(r.DeliveryDays),
                    C(r.IsAddOnBay),
                    C(r.NowPriceExVat), // second Now column – matches your header
                    C(r.LocationUrl),
                    C(r.Bullets),
                    C(r.Comments)
                ));
            }
        }

        #endregion
    }
}
