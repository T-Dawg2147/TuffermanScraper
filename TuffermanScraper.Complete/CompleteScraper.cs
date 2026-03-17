using HtmlAgilityPack;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TuffermanScraper.Complete.Models;
using TuffermanScraper.Complete.Utils;

namespace TuffermanScraper.Complete
{
    public class CompleteScraper
    {
        private readonly HttpClient _http;

        // UK standard VAT multiplier (20%)
        private const decimal UkVatRate = 1.20m;

        // Product Manager ranges (typo fixed: "Shlving" → "Shelving")
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

        public CompleteScraper(HttpClient httpClient)
        {
            _http = httpClient;
        }

        /// <summary>
        /// Fetches the product page, extracts data, and returns one ShelvingRow per variant.
        /// </summary>
        public async Task<IReadOnlyList<ShelvingRow>> ScrapeProductAsync(string url)
        {
            Console.WriteLine($"Scraping product page: {url}");
            var html = await _http.GetStringAsync(url);
            return ExtractFromHtml(html, url);
        }

        /// <summary>
        /// Main extraction method: takes the full HTML and product URL,
        /// returns one ShelvingRow per variant.
        /// </summary>
        public IReadOnlyList<ShelvingRow> ExtractFromHtml(string html, string productUrl)
        {
            var rows = new List<ShelvingRow>();

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // --- Parse Shopify product JSON (JsonDocument approach to avoid crashes) ---
            var (productTitle, productVendor, descriptionHtml, variants) = ParseProductJson(doc);

            if (variants == null || variants.Count == 0)
            {
                Console.WriteLine("  [WARN] No variants found in product JSON.");
                return rows;
            }

            // --- Flatten description HTML into searchable text ---
            var descriptionText = ExtractDescriptionText(descriptionHtml ?? string.Empty);

            // Also append delivery tab text if not already included
            var deliveryTabText = ExtractDeliveryTabText(doc);
            if (!string.IsNullOrEmpty(deliveryTabText) &&
                !descriptionText.Contains(deliveryTabText, StringComparison.OrdinalIgnoreCase))
            {
                descriptionText = string.IsNullOrEmpty(descriptionText)
                    ? deliveryTabText
                    : descriptionText + " | " + deliveryTabText;
            }

            var allText = ((productTitle ?? "") + " " + descriptionText).ToLowerInvariant();

            // --- Page-level fields ---
            var range = DetectRange(productTitle, descriptionText);
            var supplier = productVendor ?? "";
            var shelfType = DetectShelfType(descriptionText);
            var finish = DetectFinish(descriptionText);
            var assembly = DetectAssembly(descriptionText);
            var isAddOnBay = DetectIsAddOnBay(productTitle, descriptionText);
            var addOnBayStr = isAddOnBay ? "Yes" : "No";

            // Component from add-on bay / starter bay detection
            string? component = null;
            if (isAddOnBay)
                component = "Add-on bay";
            else if (allText.Contains("starter bay"))
                component = "Starter bay";

            var heightMm = DetectHeightFromTitle(productTitle) ?? DetectHeightFromUrl(productUrl);

            // Warranty: Storalex = 5 years, else scan text, default 1
            int? warrantyYears;
            if (supplier.Equals("Storalex", StringComparison.OrdinalIgnoreCase))
            {
                warrantyYears = 5;
            }
            else
            {
                warrantyYears = DetectWarrantyYearsFromText(descriptionText);
                warrantyYears ??= 1;
            }

            // Delivery from HTML
            var deliveryDays = ExtractDeliveryDays(doc);

            // Number of shelves: try HTML metafields first (done per variant below)
            // Fallback: ParseShelvesFromText
            var numberOfShelvesFromText = Parsing.ParseShelvesFromText(descriptionText);

            // Upright colour: try metafield first, then detect from text
            var uprightColour = ExtractColourMetafieldValue(doc)
                ?? DetectColourFromTitleOrDescription(productTitle, descriptionText);

            // EX VAT prices from HTML (primary source)
            var (pageWasExVat, pageNowExVat) = ExtractPricesExVat(doc);

            // --- Per-variant rows ---
            foreach (var v in variants)
            {
                var (vtWidth, vtDepth, vtLoad) = ParseVariantDimensions(v);

                // Number of shelves: try HTML levels metafield, then text fallback
                var numberOfShelves = ExtractLevelsForVariant(doc) ?? numberOfShelvesFromText;

                // Max load: option3 first, then description text
                int? maxLoad = vtLoad;
                if (maxLoad == null)
                    maxLoad = Parsing.ParseMaxLoadFromText(descriptionText);

                // Prices: use HTML EX VAT prices if found; fall back to JSON inc-VAT ÷ 1.2
                decimal? wasPriceExVat = pageWasExVat ?? ToMoneyExVat(v.CompareAtPricePence);
                decimal? nowPriceExVat = pageNowExVat ?? ToMoneyExVat(v.PricePence);

                var row = new ShelvingRow
                {
                    RangeOrHeader = range,
                    NumberOfShelves = numberOfShelves,
                    Component = component,
                    ShelfType = shelfType,
                    Height = heightMm,
                    Width = vtWidth,
                    Depth = vtDepth,
                    MaxLoadPerLevel = maxLoad,
                    UprightColour = uprightColour,
                    BeamColour = null,
                    Finish = finish,
                    Assembly = assembly,
                    Supplier = string.IsNullOrWhiteSpace(supplier) ? null : supplier,
                    Was = wasPriceExVat,
                    Now = nowPriceExVat,
                    WarrantyYears = warrantyYears,
                    DeliveryDays = deliveryDays,
                    AddOnBay = addOnBayStr,
                    Now2 = nowPriceExVat,
                    Location = productUrl,
                    Bullets = descriptionText,
                    Comments = ""
                };

                rows.Add(row);
            }

            Console.WriteLine($"  -> Produced {rows.Count} variant row(s)");
            return rows;
        }

        // ---------- JSON PARSING ----------

        private record VariantData(
            string? Title,
            string? Option1,
            string? Option2,
            string? Option3,
            int PricePence,
            int? CompareAtPricePence);

        private static (string? Title, string? Vendor, string? DescriptionHtml, List<VariantData>? Variants)
            ParseProductJson(HtmlDocument doc)
        {
            var scriptNode = doc.DocumentNode
                .SelectSingleNode("//script[@type='application/json' and @data-product-json]");

            if (scriptNode == null)
                return (null, null, null, null);

            var json = scriptNode.InnerText;
            if (string.IsNullOrWhiteSpace(json))
                return (null, null, null, null);

            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (!root.TryGetProperty("product", out var productEl))
                    return (null, null, null, null);

                string? title = productEl.TryGetProperty("title", out var tEl) ? tEl.GetString() : null;
                string? vendor = productEl.TryGetProperty("vendor", out var vEl) ? vEl.GetString() : null;
                string? descHtml = productEl.TryGetProperty("description", out var dEl) ? dEl.GetString() : null;

                var variants = new List<VariantData>();

                if (productEl.TryGetProperty("variants", out var variantsEl) &&
                    variantsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ve in variantsEl.EnumerateArray())
                    {
                        string? vTitle = ve.TryGetProperty("title", out var vtEl) ? vtEl.GetString() : null;
                        string? opt1 = ve.TryGetProperty("option1", out var o1El) ? o1El.GetString() : null;
                        string? opt2 = ve.TryGetProperty("option2", out var o2El) ? o2El.GetString() : null;
                        string? opt3 = ve.TryGetProperty("option3", out var o3El) ? o3El.GetString() : null;

                        int price = ve.TryGetProperty("price", out var priceEl) ? ParseJsonInt(priceEl) : 0;
                        int? compareAt = null;
                        if (ve.TryGetProperty("compare_at_price", out var capEl) &&
                            capEl.ValueKind != JsonValueKind.Null)
                            compareAt = ParseJsonInt(capEl);

                        variants.Add(new VariantData(vTitle, opt1, opt2, opt3, price, compareAt));
                    }
                }

                return (title, vendor, descHtml, variants);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [WARN] Failed to parse product JSON: {ex.Message}");
                return (null, null, null, null);
            }
        }

        private static int ParseJsonInt(JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.Number)
                return el.GetInt32();

            if (el.ValueKind == JsonValueKind.String &&
                int.TryParse(el.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                return v;

            return 0;
        }

        // ---------- DIMENSION PARSING ----------

        /// <summary>
        /// Parses width, depth, and load from a variant's options and title.
        /// Priority: option1/option2/option3, then variant title "W / D / Load".
        /// </summary>
        private static (int? Width, int? Depth, int? Load) ParseVariantDimensions(VariantData v)
        {
            // Try option1 (width), option2 (depth), option3 (load) first
            int? width = SafeParseInt(v.Option1);
            int? depth = SafeParseInt(v.Option2);
            int? load = ParseLoad(v.Option3);

            // If option1 didn't parse cleanly (e.g. concatenated dims), try variant title
            if (width == null || depth == null)
            {
                var (titleWidth, titleDepth, titleLoad) = ParseVariantTitle(v.Title);
                if (width == null) width = titleWidth;
                if (depth == null) depth = titleDepth;
                if (load == null) load = titleLoad;
            }

            return (width, depth, load);
        }

        /// <summary>
        /// Parses a Shopify variant title like "1500 / 450 / 200kg" into (Width, Depth, LoadKg).
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

            if (width == null && depth == null)
                return (null, null, null);

            return (width, depth, load);
        }

        private static int? SafeParseInt(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return null;
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
                ? v
                : null;
        }

        private static int? ParseLoad(string? option3)
        {
            if (string.IsNullOrWhiteSpace(option3))
                return null;

            var digits = new string(option3.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out var value) ? value : null;
        }

        // ---------- HTML EXTRACTION ----------

        /// <summary>
        /// Flattens all text nodes from the Shopify description HTML into " | " separated string.
        /// </summary>
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
        /// Extracts text from any delivery/returns tab in the full page HTML.
        /// </summary>
        private static string ExtractDeliveryTabText(HtmlDocument doc)
        {
            var selectors = new[]
            {
                "//div[@id='tab-delivery']",
                "//div[contains(@class,'tab-content') and contains(@class,'delivery')]",
                "//div[@data-tab='delivery']",
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

        /// <summary>
        /// Reads EX VAT prices from the HTML price-list/ex-vat block.
        /// Returns (Was, Now) ex-vat decimals, or (null, null) if block not found.
        /// </summary>
        private static (decimal? Was, decimal? Now) ExtractPricesExVat(HtmlDocument doc)
        {
            var exVatBlock = doc.DocumentNode.SelectSingleNode(
                "//*[contains(@class,'price-list') and contains(@class,'ex-vat')]"
            );

            if (exVatBlock == null)
                return (null, null);

            var wasNode = exVatBlock.SelectSingleNode(
                ".//span[contains(@class,'price--compare')]//span[contains(@class,'price-js')]");

            var nowNode = exVatBlock.SelectSingleNode(
                ".//span[contains(@class,'price--highlight')]//span[contains(@class,'price-js')]");

            var was = Parsing.ToDecimalOrNull(wasNode?.InnerText);
            var now = Parsing.ToDecimalOrNull(nowNode?.InnerText);

            return (was, now);
        }

        /// <summary>
        /// Extracts colour from metafield colour swatch in the page HTML.
        /// </summary>
        private static string? ExtractColourMetafieldValue(HtmlDocument doc)
        {
            var labelNode = doc.DocumentNode.SelectSingleNode(
                "//span[contains(@class,'metafield-variant_title') and normalize-space(text())='Colour:']"
            );

            if (labelNode == null)
                return null;

            var container = labelNode
                .ParentNode?
                .ParentNode?
                .SelectSingleNode(".//div[contains(@class,'metafield_variant_items')]");

            if (container == null)
                return null;

            var colourLabel = container.SelectSingleNode(".//label[contains(@class,'color-swatch__item')]");
            if (colourLabel == null)
                return null;

            var titleAttr = colourLabel.GetAttributeValue("title", string.Empty);
            if (!string.IsNullOrWhiteSpace(titleAttr))
                return titleAttr.Trim();

            var style = colourLabel.GetAttributeValue("style", string.Empty);
            if (!string.IsNullOrWhiteSpace(style))
            {
                var parts = style.Split(':', ';');
                if (parts.Length >= 2)
                    return parts[1].Trim();
            }

            return null;
        }

        /// <summary>
        /// Extracts number of levels/shelves from the HTML metafields section.
        /// </summary>
        private static int? ExtractLevelsForVariant(HtmlDocument doc)
        {
            var levelsNode = doc.DocumentNode.SelectSingleNode(
                "//span[@class='product-form__option-text text--strong' and contains(., 'Levels:')]"
            );

            if (levelsNode == null)
                return null;

            var valNode = levelsNode.SelectSingleNode(
                "./following-sibling::a[1]//span[contains(@class,'metafield_variant')]");

            if (valNode == null)
                return null;

            var text = HtmlEntity.DeEntitize(valNode.InnerText).Trim();
            return Parsing.ToIntOrNull(text);
        }

        /// <summary>
        /// Reads delivery days from product indicators div and delivery tab.
        /// Returns 1 for "24hr"/"next day", else null.
        /// </summary>
        private static int? ExtractDeliveryDays(HtmlDocument doc)
        {
            var indicators = doc.DocumentNode.SelectSingleNode(
                "//*[contains(@class,'product-indicators')]"
            );
            var indicatorText = indicators != null
                ? HtmlEntity.DeEntitize(indicators.InnerText).ToLowerInvariant()
                : string.Empty;

            var deliveryTab = doc.DocumentNode.SelectSingleNode("//*[@id='tab-delivery']");
            var deliveryText = deliveryTab != null
                ? HtmlEntity.DeEntitize(deliveryTab.InnerText).ToLowerInvariant()
                : string.Empty;

            var all = indicatorText + " " + deliveryText;

            if (string.IsNullOrWhiteSpace(all))
                return null;

            if (all.Contains("24hr") || all.Contains("24 hr") || all.Contains("next day"))
                return 1;

            return null;
        }

        // ---------- FIELD DETECTION ----------

        private static string DetectRange(string? title, string description)
        {
            var haystack = ((title ?? "") + " " + (description ?? ""))
                .ToLowerInvariant();

            // 1. Exact substring match against full range names
            foreach (var range in AllRanges)
            {
                if (haystack.Contains(range.ToLowerInvariant()))
                    return range;
            }

            // 2. Fuzzy alias matches (keyword → canonical range name)
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
            return int.TryParse(num, out var height) ? height : null;
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
            if (description.Contains("powder coated", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("powder-coated", StringComparison.OrdinalIgnoreCase))
                return "Powder Coated";

            if (description.Contains("epoxy", StringComparison.OrdinalIgnoreCase))
                return "Epoxy";

            if (description.Contains("galvanised", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("galvanized", StringComparison.OrdinalIgnoreCase))
                return "Galvanised";

            if (description.Contains("stainless steel", StringComparison.OrdinalIgnoreCase))
                return "Stainless Steel";

            if (description.Contains("nickel chrome", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("chrome shelving", StringComparison.OrdinalIgnoreCase))
                return "Nickel Chrome";

            // Generic chrome catch-all (after more specific checks)
            if (description.Contains("chrome", StringComparison.OrdinalIgnoreCase))
                return "Chrome";

            return "";
        }

        private static string DetectAssembly(string description)
        {
            var lower = description.ToLowerInvariant();

            if (lower.Contains("boltless") || lower.Contains("bolt-free") || lower.Contains("bolt free"))
                return "Self";

            if (lower.Contains("self assembly") || lower.Contains("self-assembly"))
                return "Self";

            if (lower.Contains("no nuts and bolts") || lower.Contains("no nuts") || lower.Contains("no bolts"))
                return "Self";

            if (lower.Contains("flat packed") && (lower.Contains("assembly") || lower.Contains("assemble")))
                return "Self";

            if (lower.Contains("fully assembled") || lower.Contains("comes assembled"))
                return "Fully Assembled";

            return "";
        }

        private static bool DetectIsAddOnBay(string? title, string description)
        {
            var haystack = ((title ?? "") + " " + description).ToLowerInvariant();
            return haystack.Contains("extension bay") ||
                   haystack.Contains("add-on bay") ||
                   haystack.Contains("add on bay");
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

        private static int? DetectWarrantyYearsFromText(string description)
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

        // ---------- PRICE HELPERS ----------

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
    }
}
