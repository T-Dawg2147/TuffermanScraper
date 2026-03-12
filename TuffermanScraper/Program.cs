using HtmlAgilityPack;

namespace TuffermanScraper
{
    internal class Program
    {
        private static readonly HttpClient Http = new()
        {
            BaseAddress = new Uri("https://www.tufferman.co.uk"),
        };

        private const string AllShelvingUrl = "https://www.tufferman.co.uk/collections/all-shelving";
        private const int totalPages = 36;

        public static async Task Main(string[] args)
        {
            Http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "ShelvingDataCollector/1.0 (+internal use; contact your-email@yourcompany.com)");

            var scraper = new TuffermanScraper(Http);

            var testUrl = new Uri("https://www.tufferman.co.uk/products/3x-vrs-heavy-duty-shelving-1800mm-high-200-280kg-grey");

            var variants = await scraper.ScrapeProductPageAsync(testUrl);

            Console.WriteLine("Variants scraped:");
            foreach (var v in variants)
            {
                Console.WriteLine(
                    $"{v.BaseTitle} | H={v.HeightMm} W={v.WidthMm} D={v.DepthMm} " +
                    $"Load={v.LoadPerShelfKg} Levels={v.Levels} Units={v.Units} Colour={v.Colour} " +
                    $"Was={v.WasPriceExVat} Now={v.NowPriceExVat}");
            }

            Console.WriteLine($"Total variants: {variants.Count}");
        }

        public static async Task Main_other(string[] args)
        {
            Http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "ShelvingDataCollector/1.0 (+internal use; contact harryc@slingsby.com)");

            var scraper = new TuffermanScraper(Http);

            var allProductUrls = await scraper.GetAllProductUrlsAsync(AllShelvingUrl, totalPages);

            foreach (var url in allProductUrls)
            {
                Console.WriteLine(url);
            }

            Console.WriteLine($"Done. Total product URLs: {allProductUrls.Count}");
        }
    }
}
