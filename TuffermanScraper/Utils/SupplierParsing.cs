using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TuffermanScraper.Test.Utils
{
    public static class SupplierParsing
    {
        private static readonly Regex SupplierTokenRegex =
            new(@"([A-Za-z0-9]+)_(logo|5[_ ]year|five[_ ]year|guarantee)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string? ExtractSupplier(HtmlDocument doc)
        {
            var bannerImg = doc.DocumentNode.SelectSingleNode(
                "//div[contains(@class,'product-details__right__banner')]//img");

            if (bannerImg == null)
                return null;

            var src = bannerImg.GetAttributeValue("src", null);
            if (string.IsNullOrWhiteSpace(src))
                return null;

            try
            {
                var uri = new Uri(src.StartsWith("//") ? "https:" + src : src);
                var fileName = Path.GetFileName(uri.LocalPath);

                if (string.IsNullOrEmpty(fileName))
                    return null;

                var m = SupplierTokenRegex.Match(fileName);
                if (m.Success)
                    return NormalizeName(m.Groups[1].Value);

                var baseName = Path.GetFileNameWithoutExtension(fileName);
                var firstToken = baseName.Split('_', '-')[0];
                return NormalizeName(firstToken);
            }
            catch
            {
                return null;
            }
        }

        private static string NormalizeName(string raw)
        {
            raw = raw.Trim('_', '-', ' ');
            if (string.IsNullOrEmpty(raw))
                return raw;

            return char.ToUpperInvariant(raw[0]) + raw[1..].ToLowerInvariant();
        }
    }
}
