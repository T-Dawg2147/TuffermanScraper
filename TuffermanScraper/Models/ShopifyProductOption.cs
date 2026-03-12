using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TuffermanScraper.Test.Models
{
    public sealed class ShopifyProductOption
    {
        [JsonPropertyName("position")]
        public int Position { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("values")]
        public List<string> Values { get; set; } = [];
    }
}
