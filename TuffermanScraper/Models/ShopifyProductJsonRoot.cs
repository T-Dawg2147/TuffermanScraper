using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TuffermanScraper.Test.Models
{
    public sealed class ShopifyProductJsonRoot
    {
        [JsonPropertyName("product")]
        public ShopifyProduct? Product { get; set; }
    }
}
