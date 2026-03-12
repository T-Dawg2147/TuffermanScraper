using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TuffermanScraper.Test.Models
{
    public sealed class ShopifyProduct
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("vendor")]
        public string? Vendor { get; set; }

        [JsonPropertyName("description")]
        public string? DescriptionHtml { get; set; }

        [JsonPropertyName("options")]
        public List<ShopifyProductOption> Options { get; set; } = [];

        [JsonPropertyName("variants")]
        public List<ShopifyVariant> Variants { get; set; } = [];
    }
}
