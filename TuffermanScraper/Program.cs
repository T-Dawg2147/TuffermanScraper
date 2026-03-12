using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using TuffermanScraper.Test.Models;
using TuffermanScraper.Test.Transformers;

namespace TuffermanScraper.Test
{
    internal static class Program
    {
        private static readonly string[] TestUrls =
        {
            "https://www.tufferman.co.uk/products/3x-vrs-heavy-duty-shelving-1800mm-high-200-280kg-grey",
            "https://www.tufferman.co.uk/products/1x-vrs-shelving-unit-1800mm-high-grey-with-wham-diy-recycled-plastic-storage-boxes",
            "https://www.tufferman.co.uk/products/1x-eclipse-chrome-wire-shelving-extension-bay-1625mm-high-300kg",
            "https://www.tufferman.co.uk/products/twin-slot-wall-mounted-shelving-1000mm-wide-melamine-black"
        };

        private static async Task Main()
        {
            using var http = new HttpClient();

            http.DefaultRequestHeaders.UserAgent.Clear();
            http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

            http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-GB,en;q=0.9");
            http.DefaultRequestHeaders.ConnectionClose = false;

            var allRows = new List<TuffermanVariantRow>();

            foreach (var url in TestUrls)
            {
                Console.WriteLine($"Scraping product page: {url}");

                var html = await http.GetStringAsync(url);

                var rows = TuffermanProductExtractor.ExtractFromHtml(html, url);

                Console.WriteLine($"  -> Producted {rows.Count} variant row(s)");
                foreach (var r in rows)
                {
                    Console.WriteLine($"{r.Supplier} | H={r.HeightMm} D={r.DepthMm} W={r.WidthMm} Load={r.MaxLoadPerLevelKg} | Was={r.WasPriceExVat} Now={r.NowPriceExVat}");
                }
                allRows.AddRange(rows);
            }

            const string outputPath = "Tufferman-Test-Shelving.csv";
            TuffermanProductExtractor.WriteCsv(outputPath, allRows);

            Console.WriteLine($"Wrote {allRows.Count} rows to {outputPath}");
        }
    }
}
