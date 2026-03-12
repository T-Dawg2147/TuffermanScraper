using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TuffermanScraper.Test.Models
{
    public sealed class ShopifyVariant
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("option1")]
        public string? Option1 { get; set; }

        [JsonPropertyName("option2")]
        public string? Option2 { get; set; }

        [JsonPropertyName("option3")]
        public string? Option3 { get; set; }

        [JsonPropertyName("price")]
        public int PricePence { get; set; }

        [JsonPropertyName("compare_at_price")]
        public int? CompareAtPricePence { get; set; }
    }
}
