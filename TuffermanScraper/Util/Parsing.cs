using System.Globalization;
using System.Text.RegularExpressions;

namespace TuffermanScraper.Util
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
    }
}
