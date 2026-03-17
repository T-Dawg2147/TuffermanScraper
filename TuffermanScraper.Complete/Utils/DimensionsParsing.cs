using System.Text.RegularExpressions;

namespace TuffermanScraper.Complete.Utils
{
    public static class DimensionsParsing
    {
        private static readonly Regex HwdSuffixRegex = new(
            @"(?<h>\d+)\s*h[^0-9a-zA-Z]+(?<w>\d+)\s*w[^0-9a-zA-Z]+(?<d>\d+)\s*d",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SingleLabelRegex = new(
            @"(?<label>height|width|depth)\s*[:\-]?\s*(?<val>\d+)\s*mm",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex HxWxDOrderRegex = new(
            @"h\s*[x×]\s*w\s*[x×]\s*d[^0-9]+(?<h>\d+)\s*[x×]\s*(?<w>\d+)\s*[x×]\s*(?<d>\d+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static (int? H, int? W, int? D) ParseDimensionsFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return (null, null, null);

            text = text.ToLowerInvariant();

            int? h = null, w = null, d = null;

            var mHwdOrder = HxWxDOrderRegex.Match(text);
            if (mHwdOrder.Success)
            {
                int.TryParse(mHwdOrder.Groups["h"].Value, out var hVal);
                int.TryParse(mHwdOrder.Groups["w"].Value, out var wVal);
                int.TryParse(mHwdOrder.Groups["d"].Value, out var dVal);
                h = hVal; w = wVal; d = dVal;
            }

            if (h == null || w == null || d == null)
            {
                var mSuffix = HwdSuffixRegex.Match(text);
                if (mSuffix.Success)
                {
                    if (h == null && int.TryParse(mSuffix.Groups["h"].Value, out var hVal)) h = hVal;
                    if (w == null && int.TryParse(mSuffix.Groups["w"].Value, out var wVal)) w = wVal;
                    if (d == null && int.TryParse(mSuffix.Groups["d"].Value, out var dVal)) d = dVal;
                }
            }

            var singles = SingleLabelRegex.Matches(text);
            foreach (Match m in singles)
            {
                var label = m.Groups["label"].Value.ToLowerInvariant();
                if (!int.TryParse(m.Groups["val"].Value, out var val)) continue;

                if (label == "height" && h == null) h = val;
                if (label == "width" && w == null) w = val;
                if (label == "depth" && d == null) d = val;
            }

            return (h, w, d);
        }
    }
}
