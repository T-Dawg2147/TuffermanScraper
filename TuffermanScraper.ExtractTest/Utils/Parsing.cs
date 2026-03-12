using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TuffermanScraper.ExtractTest.Utils
{
    public static class Parsing
    {
        private static readonly Regex NonDigitRegex = new(@"\D+", RegexOptions.Compiled);
        private static readonly Regex MoneyRegex = new(@"[\d.,]+", RegexOptions.Compiled);

        public static int? ToIntOrNull(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var digits = NonDigitRegex.Replace(text, "");
            if (int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                return value;

            return null;
        }

        public static decimal? ToDecimalOrNull(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var match = MoneyRegex.Match(text);
            if (!match.Success)
                return null;

            var num = match.Value.Replace(",", "");
            if (decimal.TryParse(num, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
                return value;

            return null;
        }

        public static int? ParseShelvesFromText(string allText)
        {
            var regex = new Regex(
                @"\b(\d+)\s*(adjustable\s+)?(levels?|shelves?|shelf)\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            var match = regex.Match(allText);
            if (!match.Success) return null;

            if (int.TryParse(match.Groups[1].Value, out var n) && n > 0 && n <= 20)
                return n;

            return null;
        }
    }
}
