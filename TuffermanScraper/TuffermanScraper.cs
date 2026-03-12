using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using TuffermanScraper.Models;
using TuffermanScraper.Util;

namespace TuffermanScraper
{
    public class TuffermanScraper
    {
        private readonly HttpClient _http;

        public TuffermanScraper(HttpClient httpClient)
        {
            _http = httpClient;
        }

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

            foreach (var node in productNodes)
            {
                var linkNode =
                    node.SelectSingleNode(".//a[contains(@class,'product-item__link')]")
                    ?? node.SelectSingleNode(".//a[contains(@class,'product-item__title')]");

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

        public async Task<IReadOnlyList<Uri>> GetAllProductUrlsAsync(
            string baseCollectionUrl,
            int totalPages)
        {
            var allUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int page = 1; page <= totalPages; page++)
            {
                var url = page == 1
                    ? baseCollectionUrl
                    : $"{baseCollectionUrl}?page={page}";

                Console.WriteLine($"=== Page {page} of {totalPages}");
                var pageUrls = await GetProductUrlsFromListingAsync(url);

                foreach (var uri in pageUrls)
                {
                    allUrls.Add(uri.ToString());
                }

                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            Console.WriteLine($"Total unique product URLs found across all pages: {allUrls.Count}");
            return allUrls.Select(u => new Uri(u)).ToList();
        }

        public async Task<IReadOnlyList<TuffermanVariant>> ScrapeProductPageAsync(Uri url)
        {
            Console.WriteLine($"Scraping product page: {url}");

            var html = await _http.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // --- Base title ---
            var titleNode = doc.DocumentNode.SelectSingleNode("//h1");
            var baseTitle = titleNode?.InnerText.Trim() ?? "";

            // Optional breadcrumb/range
            var rangeOrCategory = "";
            var breadcrumbLast = doc.DocumentNode.SelectSingleNode("//nav[contains(@class,'breadcrumb')]//li[last()]");
            if (breadcrumbLast != null)
                rangeOrCategory = HtmlEntity.DeEntitize(breadcrumbLast.InnerText.Trim());

            // --- Key Features bullets ---
            var bulletsText = ExtractBullets(doc);

            // --- Global EX-VAT price block for the *currently selected* variant ---
            var (pageWasExVat, pageNowExVat) = ExtractPricesExVat(doc);

            // --- Height (single value for this page) ---
            var heightText = ExtractSingleMetafieldValue(doc, "Height (mm):");
            var heightMm = Parsing.ToIntOrNull(heightText);

            // --- Qty of Shelving Units (single value on this page) ---
            var unitsText = ExtractSingleMetafieldValue(doc, "Qty of Shelving Units:");
            var units = Parsing.ToIntOrNull(unitsText);

            // --- Colour (single value on this page) ---
            var colourText = ExtractColourMetafieldValue(doc); // e.g. "Grey" or "Blue"

            // --- Main variant <select> with width/depth/load/price per SKU ---
            var variants = new List<TuffermanVariant>();

            var selectNode = doc.DocumentNode.SelectSingleNode("//select[@name='id']");
            if (selectNode == null)
            {
                Console.WriteLine("  !! No <select name='id'> variant list found; falling back to single variant");
                // Fallback: at least return one row with page-level info
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
                    Bullets = bulletsText
                });
                return variants;
            }

            var optionNodes = selectNode.SelectNodes("./option");
            if (optionNodes == null || optionNodes.Count == 0)
            {
                Console.WriteLine("  !! <select name='id'> had no <option> children");
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
                    Bullets = bulletsText
                });
                return variants;
            }

            foreach (var opt in optionNodes)
            {
                var text = HtmlEntity.DeEntitize(opt.InnerText).Trim();
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                // Example option text:
                // "900 / 300 / 200kg - £107.99"
                // or "1200 / 300 / 280kg - £205.19"
                // We'll split around '-' and '/'.
                string? sizePart = null;
                string? pricePart = null;

                var dashSplit = text.Split('-', 2);
                if (dashSplit.Length == 2)
                {
                    sizePart = dashSplit[0].Trim();
                    pricePart = dashSplit[1].Trim();
                }
                else
                {
                    sizePart = text;
                }

                int? widthMm = null;
                int? depthMm = null;
                int? loadPerShelfKg = null;

                if (!string.IsNullOrEmpty(sizePart))
                {
                    var pieces = sizePart.Split('/')
                                         .Select(p => p.Trim())
                                         .ToArray();

                    if (pieces.Length >= 1)
                        widthMm = Parsing.ToIntOrNull(pieces[0]);              // 900
                    if (pieces.Length >= 2)
                        depthMm = Parsing.ToIntOrNull(pieces[1]);              // 300 / 450 / 600
                    if (pieces.Length >= 3)
                        loadPerShelfKg = Parsing.ToIntOrNull(pieces[2]);       // "200kg" / "280kg"
                }

                decimal? variantPriceExVat = null;
                if (!string.IsNullOrEmpty(pricePart))
                {
                    // e.g. "£107.99"
                    variantPriceExVat = Parsing.ToDecimalOrNull(pricePart);
                }

                // Some variants may not show a "was" price individually, so we use the page-level "was" price for all of them.
                var variant = new TuffermanVariant
                {
                    BaseTitle = baseTitle,
                    RangeOrCategory = rangeOrCategory,
                    HeightMm = heightMm,
                    WidthMm = widthMm,
                    DepthMm = depthMm,
                    LoadPerShelfKg = loadPerShelfKg,
                    Levels = ExtractLevelsForVariant(doc, opt),    // simple helper below
                    Units = units,
                    Colour = colourText ?? "",
                    WasPriceExVat = pageWasExVat,
                    NowPriceExVat = variantPriceExVat ?? pageNowExVat,
                    Url = url.ToString(),
                    Bullets = bulletsText,
                    Supplier = "Tufferman"
                };

                variants.Add(variant);
            }

            Console.WriteLine($"  -> Produced {variants.Count} variant row(s)");
            return variants;
        }

        #region Helpers

        private static string ExtractBullets(HtmlDocument doc)
        {
            // Look for a Key Features heading and then the following <ul>
            var heading = doc.DocumentNode.SelectSingleNode(
                "//h2[contains(translate(., 'KEYFEATURES', 'keyfeatures'), 'key features')]"
            );

            HtmlNode? list = null;

            if (heading != null)
            {
                list = heading.SelectSingleNode("following-sibling::ul[1]");
            }
            if (list == null)
            {
                // Fallback: any <ul> inside a key-features-like container
                list = doc.DocumentNode.SelectSingleNode(
                    "//*[contains(translate(@class, 'KEYFEATURES', 'keyfeatures'), 'key-features')]//ul"
                );
            }

            if (list == null)
                return "";

            var items = list.SelectNodes(".//li");
            if (items == null || items.Count == 0)
                return "";

            // Join bullets with " | " so it fits nicely into one CSV cell
            var bulletTexts = items
                .Select(li => HtmlEntity.DeEntitize(li.InnerText.Trim()))
                .Where(t => !string.IsNullOrWhiteSpace(t));

            return string.Join(" | ", bulletTexts);
        }

        private static (decimal? Was, decimal? Now) ExtractPricesExVat(HtmlDocument doc)
        {
            // Ex VAT price block – based on tile markup you shared
            var exVatBlock = doc.DocumentNode.SelectSingleNode(
                "//*[contains(@class,'price-list') and contains(@class,'ex-vat') and contains(@class,'active')]"
            );

            if (exVatBlock == null)
                return (null, null);

            // "was" price is usually in a price--compare span
            var wasNode = exVatBlock.SelectSingleNode(".//span[contains(@class,'price--compare')]//span[contains(@class,'price-js')]");
            var nowNode = exVatBlock.SelectSingleNode(".//span[contains(@class,'price--highlight')]//span[contains(@class,'price-js')]");

            var was = Parsing.ToDecimalOrNull(wasNode?.InnerText);
            var now = Parsing.ToDecimalOrNull(nowNode?.InnerText);

            return (was, now);
        }

        /// <summary>
        /// Extracts textual option values for a labelled option group, e.g. "Height (mm)".
        /// Returns the inner text of each clickable element (button/span/a) in that group.
        /// </summary>
        private static List<string?> ExtractOptionValues(HtmlDocument doc, string labelText, bool allowText = false)
        {
            var results = new List<string?>();

            // Find any element whose text contains the label text, then move to the next sibling container
            var labelNode = doc.DocumentNode.SelectSingleNode(
                $"//*[contains(normalize-space(translate(., '{labelText.ToUpper()}', '{labelText.ToLower()}')), '{labelText.ToLower()}')]"
            );

            if (labelNode == null)
                return results;

            // Often options are in the next sibling div
            var container = labelNode.SelectSingleNode("following-sibling::*[1]");
            if (container == null)
                container = labelNode.ParentNode;

            if (container == null)
                return results;

            // Buttons or spans that represent options
            var optionNodes = container.SelectNodes(".//button | .//a | .//span");
            if (optionNodes == null)
                return results;

            foreach (var node in optionNodes)
            {
                var text = HtmlEntity.DeEntitize(node.InnerText).Trim();
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                // Filter out non-value text if this is a numeric option (unless allowText is true)
                if (!allowText && Parsing.ToIntOrNull(text) == null)
                    continue;

                results.Add(text);
            }

            // Deduplicate
            return results.Distinct().ToList();
        }

        private static List<string> ExtractColourOptions(HtmlDocument doc)
        {
            var colours = new List<string>();

            // First try colour swatch buttons/links
            var swatchNodes = doc.DocumentNode.SelectNodes(
                "//*[contains(@class,'color-swatch')]/span[contains(@class,'color-swatch__item')]"
            );

            if (swatchNodes != null)
            {
                foreach (var node in swatchNodes)
                {
                    var style = node.GetAttributeValue("style", null);
                    // e.g. "background-color: blue"
                    if (!string.IsNullOrWhiteSpace(style))
                    {
                        var parts = style.Split(':', ';');
                        if (parts.Length >= 2)
                        {
                            var colour = parts[1].Trim();
                            if (!string.IsNullOrEmpty(colour))
                                colours.Add(colour);
                        }
                    }
                }
            }

            // Fallback: some pages might have a "Colour" option group like height/width
            if (colours.Count == 0)
            {
                var colourTexts = ExtractOptionValues(doc, "Colour", allowText: true)
                    .Where(c => !string.IsNullOrWhiteSpace(c))!
                    .ToList();

                colours.AddRange(colourTexts);
            }

            return colours.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string? ExtractSingleMetafieldValue(HtmlDocument doc, string labelText)
        {
            // Find span with that exact label text
            var labelNode = doc.DocumentNode.SelectSingleNode(
                $"//span[contains(@class,'metafield-variant_title') and normalize-space(text())='{labelText}']"
            );

            if (labelNode == null)
                return null;

            // Value is in a span.metafield_variant inside the following .metafield_variant_items
            var container = labelNode
                .ParentNode?                           // product-form__option-name__container
                .ParentNode?                           // metafield_variant_selector
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
            // Colour section has metafield-variant_title "Colour:"
            var labelNode = doc.DocumentNode.SelectSingleNode(
                "//span[contains(@class,'metafield-variant_title') and normalize-space(text())='Colour:']"
            );

            if (labelNode == null)
                return null;

            // Look for label.color-swatch__item with a title attribute
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

            // Fallback: use background-color value
            var style = colourLabel.GetAttributeValue("style", null);
            if (!string.IsNullOrWhiteSpace(style))
            {
                var parts = style.Split(':', ';');
                if (parts.Length >= 2)
                    return parts[1].Trim();
            }

            return null;
        }

        // For now, levels are always "4" in your snippet; we can keep it simple.
        private static int? ExtractLevelsForVariant(HtmlDocument doc, HtmlNode optionNode)
        {
            // Each option has an ID we could tie to the upgrade_... sections,
            // but your snippet shows "Levels: 4" for all of them, so we just parse one.
            var levelsNode = doc.DocumentNode.SelectSingleNode(
                "//span[@class='product-form__option-text text--strong' and contains(., 'Levels:')]"
            );

            if (levelsNode == null)
                return null;

            var valNode = levelsNode.SelectSingleNode("./following-sibling::a[1]//span[contains(@class,'metafield_variant')]");
            if (valNode == null)
                return Parsing.ToIntOrNull("4"); // safe fallback

            var text = HtmlEntity.DeEntitize(valNode.InnerText).Trim();
            return Parsing.ToIntOrNull(text);
        }

        #endregion
    }
}
