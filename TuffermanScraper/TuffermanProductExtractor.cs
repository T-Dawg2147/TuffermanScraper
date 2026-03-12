using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using TuffermanScraper.Test.Models;

namespace TuffermanScraper.Test
{
    public static class TuffermanProductExtractor
    {
        // UK standard VAT multiplier (20%)
        private const decimal UkVatRate = 1.20m;

        // Product Manager ranges
        private static readonly string[] AllRanges =
        {
            "Light Duty Shelving", "Medium Duty Shelving", "Heavy Duty Shelving",
            "Archive Shelving", "Tubular Shelving", "Mobile Shelving",
            "Longspan Shelving", "Catering Shelving", "Cantilever Racking",
            "Type Shelving", "Wall Mounted Shelving", "Retail Shelving",
            "Office Shelving", "Shelving with Boxes", "Warehouse Shelving",
            "Specialist Shelving", "Plastic Shelving", "Metal Shelving",
            "Industrial Shelving", "Boltless Shelving", "Wire Shelving"
        };

        // Fuzzy keyword → canonical range name
        private static readonly Dictionary<string, string> RangeAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            { "chrome wire",   "Wire Shelving" },
            { "wire rack",     "Wire Shelving" },
            { "longspan",      "Longspan Shelving" },
            { "long span",     "Longspan Shelving" },
            { "cantilever",    "Cantilever Racking" },
            { "wall mounted",  "Wall Mounted Shelving" },
            { "twin slot",     "Wall Mounted Shelving" },
            { "catering",      "Catering Shelving" },
            { "mobile",        "Mobile Shelving" },
            { "archive",       "Archive Shelving" },
            { "retail",        "Retail Shelving" },
            { "plastic",       "Plastic Shelving" },
            { "boltless",      "Boltless Shelving" },
            { "heavy duty",    "Heavy Duty Shelving" },
            { "medium duty",   "Medium Duty Shelving" },
            { "light duty",    "Light Duty Shelving" },
            { "industrial",    "Industrial Shelving" },
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

            // Also grab any delivery/returns tab text from the full page HTML
            var deliveryTabText = ExtractDeliveryTabText(doc);
            if (!string.IsNullOrEmpty(deliveryTabText) &&
                !descriptionText.Contains(deliveryTabText, StringComparison.OrdinalIgnoreCase))
            {
                descriptionText = string.IsNullOrEmpty(descriptionText)
                    ? deliveryTabText
                    : descriptionText + " | " + deliveryTabText;
            }

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
                // Prefer dimensions parsed from the variant title (e.g. "1500 / 450 / 200kg")
                var (vtWidth, vtDepth, vtLoad) = ParseVariantTitle(v.Title);

                var row = new TuffermanVariantRow
                {
                    Range = range,
                    NumberOfShelves = numberOfShelves,
                    Component = component,
                    ShelfType = shelfType,
                    HeightMm = height,
                    WidthMm = vtWidth ?? SafeParseInt(v.Option1),
                    DepthMm = vtDepth ?? SafeParseInt(v.Option2),
                    MaxLoadPerLevelKg = vtLoad ?? ParseLoad(v.Option3),

                    UprightColour = DetectColourFromTitleOrDescription(product.Title, descriptionText),
                    BeamColour = "", // leave blank unless you have a solid rule
                    Finish = finish,
                    Assembly = assembly,
                    Supplier = supplier,

                    // INC-VAT pence from JSON → converted to EX-VAT pounds:
                    WasPriceExVat = ToMoneyExVat(v.CompareAtPricePence),
                    NowPriceExVat = ToMoneyExVat(v.PricePence),

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
                PropertyNameCaseInsensitive = true,
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
            };

            try
            {
                // Deserialize into a raw JsonDocument first, then extract just
                // the parts we care about — avoids failures from unmapped fields.
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (!root.TryGetProperty("product", out var productElement))
                    return null;

                var result = new ShopifyProductJsonRoot
                {
                    Product = new ShopifyProduct()
                };

                var product = result.Product;

                if (productElement.TryGetProperty("id", out var idEl))
                    product.Id = idEl.GetInt64();

                if (productElement.TryGetProperty("title", out var titleEl))
                    product.Title = titleEl.GetString();

                if (productElement.TryGetProperty("vendor", out var vendorEl))
                    product.Vendor = vendorEl.GetString();

                if (productElement.TryGetProperty("description", out var descEl))
                    product.DescriptionHtml = descEl.GetString();

                // Parse variants — these are the ones we actually need
                if (productElement.TryGetProperty("variants", out var variantsEl) &&
                    variantsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ve in variantsEl.EnumerateArray())
                    {
                        var variant = new ShopifyVariant();

                        if (ve.TryGetProperty("id", out var vid))
                            variant.Id = vid.GetInt64();

                        if (ve.TryGetProperty("title", out var vt))
                            variant.Title = vt.GetString();

                        if (ve.TryGetProperty("option1", out var o1))
                            variant.Option1 = o1.GetString();

                        if (ve.TryGetProperty("option2", out var o2))
                            variant.Option2 = o2.GetString();

                        if (ve.TryGetProperty("option3", out var o3))
                            variant.Option3 = o3.GetString();

                        if (ve.TryGetProperty("price", out var priceEl))
                            variant.PricePence = ParseJsonInt(priceEl);

                        if (ve.TryGetProperty("compare_at_price", out var capEl) &&
                            capEl.ValueKind != JsonValueKind.Null)
                            variant.CompareAtPricePence = ParseJsonInt(capEl);

                        product.Variants.Add(variant);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [WARN] Failed to parse product JSON: {ex.Message}");
                return null;
            }
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

        /// <summary>
        /// Extracts text from any delivery/returns tab content in the full page HTML
        /// that might not be included in the Shopify product JSON description.
        /// </summary>
        private static string ExtractDeliveryTabText(HtmlDocument doc)
        {
            // Try several common selectors for delivery tab content
            var selectors = new[]
            {
                "//div[contains(@class,'tab-content')]//*[contains(translate(text(),'DELIVERY','delivery'),'delivery')]/..",
                "//div[contains(@class,'tab-content') and contains(@class,'delivery')]",
                "//div[@data-tab='delivery']",
                "//div[contains(@class,'tab-panel')]//div[contains(@class,'delivery')]",
            };

            HtmlNode? tabNode = null;
            foreach (var selector in selectors)
            {
                try
                {
                    tabNode = doc.DocumentNode.SelectSingleNode(selector);
                    if (tabNode != null)
                        break;
                }
                catch
                {
                    // XPath might fail on some selectors; skip gracefully
                }
            }

            if (tabNode == null)
                return string.Empty;

            var sb = new StringBuilder();
            foreach (var node in tabNode.Descendants()
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

        #region Variant title parsing

        /// <summary>
        /// Parses a Shopify variant title like "1500 / 450 / 200kg" into (Width, Depth, LoadKg).
        /// Returns (null, null, null) if the title doesn't match the expected pattern.
        /// </summary>
        private static (int? Width, int? Depth, int? LoadKg) ParseVariantTitle(string? variantTitle)
        {
            if (string.IsNullOrWhiteSpace(variantTitle))
                return (null, null, null);

            var parts = variantTitle.Split(" / ", StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return (null, null, null);

            int? width = SafeParseInt(parts[0].Trim());
            int? depth = SafeParseInt(parts[1].Trim());
            int? load = parts.Length >= 3 ? ParseLoad(parts[2].Trim()) : null;

            // Only return values if at least width and depth parsed successfully
            if (width == null && depth == null)
                return (null, null, null);

            return (width, depth, load);
        }

        #endregion

        #region field detection

        private static string DetectRange(string? title, string description)
        {
            var haystack = ((title ?? "") + " " + (description ?? ""))
                .ToLowerInvariant();

            // 1. Try exact substring match against the full range names
            foreach (var range in AllRanges)
            {
                var norm = range.ToLowerInvariant();
                if (haystack.Contains(norm))
                    return range;
            }

            // 2. Try fuzzy alias matches (keyword → canonical range name)
            foreach (var kvp in RangeAliases)
            {
                if (haystack.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }

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

        /// <summary>
        /// Converts INC-VAT pence to EX-VAT pounds, rounded to 2dp.
        /// </summary>
        private static decimal? ToMoneyExVat(int? pence)
        {
            if (pence == null || pence <= 0)
                return null;
            var incVatPounds = pence.Value / 100m;
            return Math.Round(incVatPounds / UkVatRate, 2, MidpointRounding.AwayFromZero);
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
                fullHtml.Contains("next day delivery", StringComparison.OrdinalIgnoreCase) ||
                fullHtml.Contains("free next day", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (fullHtml.Contains("1-2 working days", StringComparison.OrdinalIgnoreCase))
                return 2;

            if (fullHtml.Contains("2-3 working days", StringComparison.OrdinalIgnoreCase))
                return 3;

            if (fullHtml.Contains("within 4 working days", StringComparison.OrdinalIgnoreCase))
                return 4;

            if (fullHtml.Contains("3-5 working days", StringComparison.OrdinalIgnoreCase) ||
                fullHtml.Contains("within 5 working days", StringComparison.OrdinalIgnoreCase))
                return 5;

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

                var shelvesToken2 = levels + " shelves";
                if (description.Contains(shelvesToken2, StringComparison.OrdinalIgnoreCase))
                    return levels;

                var shelfLevels = levels + " shelf levels";
                if (description.Contains(shelfLevels, StringComparison.OrdinalIgnoreCase))
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

            if (description.Contains("chipboard", StringComparison.OrdinalIgnoreCase))
                return "Solid/Chipboard/Steel";

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

            if (description.Contains("galvanised", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("galvanized", StringComparison.OrdinalIgnoreCase))
                return "Galvanised";

            // Generic "chrome" catch-all (after the more specific "nickel chrome" check)
            if (description.Contains("chrome", StringComparison.OrdinalIgnoreCase))
                return "Chrome";

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
            if (haystack.Contains("silver"))
                return "Silver";
            if (haystack.Contains("red"))
                return "Red";
            if (haystack.Contains("orange"))
                return "Orange";

            return "";
        }

        private static int ParseJsonInt(JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.Number)
                return el.GetInt32();

            if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                return v;

            return 0;                
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