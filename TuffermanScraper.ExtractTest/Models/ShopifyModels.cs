using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TuffermanScraper.ExtractTest.Models
{
    /// <summary>
    /// Root of the JSON blob found in:
    ///   &lt;script type="application/json" data-product-json&gt;...&lt;/script&gt;
    /// </summary>
    public sealed class ShopifyProductJsonRoot
    {
        [JsonPropertyName("product")]
        public ShopifyProduct? Product { get; set; }
    }

    public sealed class ShopifyProduct
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("vendor")]
        public string? Vendor { get; set; }

        // NOTE: Shopify sends HTML here; we’ll strip tags with HtmlAgilityPack.
        [JsonPropertyName("description")]
        public string? DescriptionHtml { get; set; }

        [JsonPropertyName("variants")]
        public List<ShopifyVariant> Variants { get; set; } = new();
    }

    public sealed class ShopifyVariant
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        // On Tufferman this is "Width (mm)".
        [JsonPropertyName("option1")]
        public string? Option1 { get; set; }

        // On Tufferman this is "Depth (mm)".
        [JsonPropertyName("option2")]
        public string? Option2 { get; set; }

        // On Tufferman this is "Load Per Shelf (UDL)" (e.g. "200kg").
        [JsonPropertyName("option3")]
        public string? Option3 { get; set; }

        // IMPORTANT: Shopify price fields are *VAT-inclusive* and in pence.
        // We DO NOT use them for the EX-VAT pricing in your sheet – that still
        // comes from your existing HTML-based price scraping.
        [JsonPropertyName("price")]
        public int PricePence { get; set; }

        [JsonPropertyName("compare_at_price")]
        public int? CompareAtPricePence { get; set; }
    }
}