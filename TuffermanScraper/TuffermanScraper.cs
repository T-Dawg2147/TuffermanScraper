using HtmlAgilityPack;
using TuffermanScraper.Test.Models;
using TuffermanScraper.Test.Utils;

namespace TuffermanScraper
{
    public class TuffermanScraper
    {
        private readonly HttpClient _http;

        public TuffermanScraper(HttpClient httpClient)
        {
            _http = httpClient;
        }

        // ---------- LISTING PAGES ----------

        public async Task<IReadOnlyList<Uri>> GetProductUrlsFromListingAsync(string listingUrl)
        {
            var html = await _http.GetStringAsync(listingUrl);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var productUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var productNodes = doc.DocumentNode.SelectNodes("//div[contains(@class,'product-item ')]");
            if (productNodes == null)
            {
                Console.WriteLine("No product-item divs found on " + listingUrl);
                return Array.Empty<Uri>();
            }

            foreach (var productNode in productNodes)
            {
                var linkNode =
                    productNode.SelectSingleNode(".//a[contains(@class,'product-item__link')]") ??
                    productNode.SelectSingleNode(".//a[contains(@class,'product-item__title')]");

                if (linkNode == null)
                    continue;

                var href = linkNode.GetAttributeValue("href", null);
                if (string.IsNullOrWhiteSpace(href))
                    continue;

                if (href.StartsWith("/"))
                    href = "https://www.tufferman.co.uk" + href;

                productUrls.Add(href);
            }

            Console.WriteLine($"Found {productUrls.Count} product URL(s) on {listingUrl}");
            return productUrls.Select(u => new Uri(u)).ToList();
        }

        public async Task<IReadOnlyList<Uri>> GetAllProductUrlsAsync(string baseCollectionUrl, int totalPages)
        {
            var allUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int page = 1; page <= totalPages; page++)
            {
                var url = page == 1 ? baseCollectionUrl : $"{baseCollectionUrl}?page={page}";
                Console.WriteLine($"=== Page {page} of {totalPages} ===");

                var pageUrls = await GetProductUrlsFromListingAsync(url);
                foreach (var uri in pageUrls)
                    allUrls.Add(uri.ToString());

                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            Console.WriteLine($"Total unique product URLs found across all pages: {allUrls.Count}");
            return allUrls.Select(u => new Uri(u)).ToList();
        }

        // ---------- PRODUCT PAGES ----------

        public async Task<IReadOnlyList<TuffermanVariant>> ScrapeProductPageAsync(Uri url)
        {
            Console.WriteLine($"Scraping product page: {url}");

            var html = await _http.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var supplier = SupplierParsing.ExtractSupplier(doc) ?? string.Empty;

            var titleNode = doc.DocumentNode.SelectSingleNode("//h1");
            var baseTitle = titleNode?.InnerText.Trim() ?? "";

            var rangeOrCategory = "";
            var breadcrumbLast = doc.DocumentNode.SelectSingleNode("//nav[contains(@class,'breadcrumb')]//li[last()]");
            if (breadcrumbLast != null)
                rangeOrCategory = HtmlEntity.DeEntitize(breadcrumbLast.InnerText.Trim());

            var bulletsText = ExtractBullets(doc);

            var (pageWasExVat, pageNowExVat) = ExtractPricesExVat(doc);
            Console.WriteLine($"  Prices (EX VAT): WAS={pageWasExVat} NOW={pageNowExVat}");

            var heightText = ExtractSingleMetafieldValue(doc, "Height (mm):");
            var heightMm = Parsing.ToIntOrNull(heightText);

            var unitsText = ExtractSingleMetafieldValue(doc, "Qty of Shelving Units:");
            var units = Parsing.ToIntOrNull(unitsText);

            var colourText = ExtractColourMetafieldValue(doc);

            var deliveryDays = ExtractDeliveryDays(doc);

            var variants = new List<TuffermanVariant>();

            var selectNode = doc.DocumentNode.SelectSingleNode("//select[@name='id']");
            if (selectNode == null)
            {
                Console.WriteLine("  !! No <select name='id'> found; returning single variant with page-level info");
                variants.Add(new TuffermanVariant
                {
                    BaseTitle = baseTitle,
                    RangeOrCategory = rangeOrCategory,
                    HeightMm = heightMm,
                    Units = units,
                    Colour = colourText ?? "",
                    WasPriceExVat = pageWasExVat,
                    NowPriceExVat = pageNowExVat,
                    Url = url.ToString(),
                    Bullets = bulletsText,
                    Supplier = supplier,
                    DeliveryDays = deliveryDays
                });
                return variants;
            }

            var optionNodes = selectNode.SelectNodes("./option");
            if (optionNodes == null || optionNodes.Count == 0)
            {
                Console.WriteLine("  !! <select name='id'> has no options; returning single variant");
                variants.Add(new TuffermanVariant
                {
                    BaseTitle = baseTitle,
                    RangeOrCategory = rangeOrCategory,
                    HeightMm = heightMm,
                    Units = units,
                    Colour = colourText ?? "",
                    WasPriceExVat = pageWasExVat,
                    NowPriceExVat = pageNowExVat,
                    Url = url.ToString(),
                    Bullets = bulletsText,
                    Supplier = supplier,
                    DeliveryDays = deliveryDays
                });
                return variants;
            }

            foreach (var opt in optionNodes)
            {
                var text = HtmlEntity.DeEntitize(opt.InnerText).Trim();
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                // e.g. "900 / 300 / 200kg - £107.99"
                // or "1800h x 900w x 450d / 200kg / 37L - £113.99"
                string? left = null;
                string? pricePart = null;

                var dashSplit = text.Split('-', 2);
                if (dashSplit.Length == 2)
                {
                    left = dashSplit[0].Trim();
                    pricePart = dashSplit[1].Trim();
                }
                else
                {
                    left = text;
                }

                int? heightFromDim = null;
                int? widthMm = null;
                int? depthMm = null;
                int? loadPerShelfKg = null;

                if (!string.IsNullOrEmpty(left))
                {
                    var pieces = left.Split('/')
                                     .Select(p => p.Trim())
                                     .ToArray();

                    if (pieces.Length > 0)
                    {
                        var dim = pieces[0];

                        if (dim.Contains('x'))
                        {
                            // "1800h x 900w x 450d"
                            var dims = dim.Split('x')
                                          .Select(d => d.Trim())
                                          .ToArray();
                            if (dims.Length >= 3)
                            {
                                heightFromDim = Parsing.ToIntOrNull(dims[0]);
                                widthMm = Parsing.ToIntOrNull(dims[1]);
                                depthMm = Parsing.ToIntOrNull(dims[2]);
                            }
                        }
                        else
                        {
                            // "900 / 300 / 200kg" style (older)
                            var dims = dim.Split('/')
                                          .Select(d => d.Trim())
                                          .ToArray();
                            if (dims.Length >= 1) widthMm = Parsing.ToIntOrNull(dims[0]);
                            if (dims.Length >= 2) depthMm = Parsing.ToIntOrNull(dims[1]);
                            if (dims.Length >= 3) loadPerShelfKg = Parsing.ToIntOrNull(dims[2]);
                        }
                    }

                    if (pieces.Length > 1)
                        loadPerShelfKg ??= Parsing.ToIntOrNull(pieces[1]);
                }

                // Fallback for depth: dedicated "Depth (mm)" group
                if (depthMm == null)
                {
                    var depthText = ExtractOptionGroupValue(doc, "Depth (mm)");
                    depthMm = Parsing.ToIntOrNull(depthText);
                }

                // Height: prefer explicit Height metafield, then dimension
                var effectiveHeight = heightMm ?? heightFromDim;
                var levels = ExtractLevelsForVariant(doc, opt);

                decimal? variantPriceExVat = null;
                if (!string.IsNullOrEmpty(pricePart))
                    variantPriceExVat = Parsing.ToDecimalOrNull(pricePart);

                var variant = new TuffermanVariant
                {
                    BaseTitle = baseTitle,
                    RangeOrCategory = rangeOrCategory,
                    HeightMm = effectiveHeight,
                    WidthMm = widthMm,
                    DepthMm = depthMm,
                    LoadPerShelfKg = loadPerShelfKg,
                    Levels = levels,
                    Units = units,
                    Colour = colourText ?? "",
                    WasPriceExVat = pageWasExVat,
                    NowPriceExVat = variantPriceExVat ?? pageNowExVat,
                    Url = url.ToString(),
                    Bullets = bulletsText,
                    Supplier = supplier,
                    DeliveryDays = deliveryDays
                };

                variants.Add(variant);
            }

            Console.WriteLine($"  -> Produced {variants.Count} variant row(s)");
            return variants;
        }

        // ---------- HELPERS ----------

        private static string ExtractBullets(HtmlDocument doc)
        {
            var tabNode = doc.DocumentNode.SelectSingleNode(
                "//*[@class='product__description-content-holder']" +
                "//*[contains(@class,'tab-content') and contains(@class,'current')]"
            );

            HtmlNode containerToUse = tabNode ?? doc.DocumentNode;

            var textNodes = containerToUse.SelectNodes(".//p | .//li | .//strong");

            if (textNodes == null || textNodes.Count == 0)
                return "";

            var parts = new List<string>();

            foreach (var node in textNodes)
            {
                var txt = HtmlEntity.DeEntitize(node.InnerText ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(txt))
                    continue;

                txt = System.Text.RegularExpressions.Regex.Replace(txt, @"\s+", " ");

                parts.Add(txt);
            }

            var distinctParts = parts
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct()
                .ToList();

            return string.Join(" | ", distinctParts);
        }

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

        private static string? ExtractSingleMetafieldValue(HtmlDocument doc, string labelText)
        {
            var labelNode = doc.DocumentNode.SelectSingleNode(
                $"//span[contains(@class,'metafield-variant_title') and normalize-space(text())='{labelText}']"
            );

            if (labelNode == null)
                return null;

            var container = labelNode
                .ParentNode?
                .ParentNode?
                .SelectSingleNode(".//div[contains(@class,'metafield_variant_items')]");

            if (container == null)
                return null;

            var valueNode = container.SelectSingleNode(".//span[contains(@class,'metafield_variant')]");
            if (valueNode == null)
                return null;

            return HtmlEntity.DeEntitize(valueNode.InnerText).Trim();
        }

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

            var titleAttr = colourLabel.GetAttributeValue("title", null);
            if (!string.IsNullOrWhiteSpace(titleAttr))
                return titleAttr.Trim();

            var style = colourLabel.GetAttributeValue("style", null);
            if (!string.IsNullOrWhiteSpace(style))
            {
                var parts = style.Split(':', ';');
                if (parts.Length >= 2)
                    return parts[1].Trim();
            }

            return null;
        }

        private static int? ExtractLevelsForVariant(HtmlDocument doc, HtmlNode optionNode)
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

        private static string? ExtractOptionGroupValue(HtmlDocument doc, string label)
        {
            var lower = label.ToLowerInvariant();

            var labelNode = doc.DocumentNode.SelectSingleNode(
                "//span[contains(@class,'product-form__option-name') and " +
                $"contains(translate(normalize-space(.), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), '{lower}')]"
            );

            if (labelNode == null)
                return null;

            var selected = labelNode.SelectSingleNode(".//span[contains(@class,'product-form__selected-value')]");
            if (selected != null)
                return HtmlEntity.DeEntitize(selected.InnerText).Trim();

            var optionContainer = labelNode.ParentNode?.ParentNode;
            if (optionContainer == null)
                return null;

            var textNode = optionContainer.SelectSingleNode(
                ".//span[contains(@class,'block-swatch__item-text')]");
            return textNode != null
                ? HtmlEntity.DeEntitize(textNode.InnerText).Trim()
                : null;
        }

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
    }
}