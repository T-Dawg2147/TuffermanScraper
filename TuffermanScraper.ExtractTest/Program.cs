using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using TuffermanScraper.ExtractTest.Models;
using TuffermanScraper.ExtractTest.Transformers;
using static System.Net.WebRequestMethods;

namespace TuffermanScraper.ExtractTest
{
    internal class Program
    {
        private static readonly HttpClient Http = new()
        {
            BaseAddress = new Uri("https://www.tufferman.co.uk"),
        };

        private const string AllShelvingUrl = "https://www.tufferman.co.uk/collections/all-shelving";
        private const int TotalPages = 36;

        public static async Task Main(string[] args)
        {
            /*Http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "ShelvingDataCollector/1.3 (+internal use; harryc@slingsby.com)");*/
            Http.DefaultRequestHeaders.UserAgent.Clear();
            Http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Chrome/122.0.0.0");
            Http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            Http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-GB,en;q=0.9");
            Http.DefaultRequestHeaders.ConnectionClose = false;


            var scraper = new TuffermanScraper(Http);

            // --- OPTION A: test on a single product page ---
            var testUrlsReadonly = scraper.GetAllProductUrlsAsync(AllShelvingUrl, TotalPages);

            var testUrls = testUrlsReadonly.Result.ToList();

            var allTestVariants = new List<TuffermanVariant>();

            foreach (var u in testUrls)
            {
                var vs = await scraper.ScrapeProductPageAsync(u);
                await Task.Delay(TimeSpan.FromSeconds(5));
                Console.WriteLine($"=== Variants for {u} ===");
                DumpVariants(vs);
                allTestVariants.AddRange(vs);
            }

            const string headerText = "Light Duty";
            var rows = new System.Collections.Generic.List<ShelvingRow>();
            foreach (var v in allTestVariants)
            {
                rows.Add(ShelvingRowTransformer.FromVariant(v, headerText));
            }

            using var writer = new System.IO.StreamWriter("Tufferman-Test-Shelving.csv");
            using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true
            });

            csv.Context.RegisterClassMap<ShelvingRowMap>();
            csv.WriteRecords(rows);

            Console.WriteLine($"Wrote {rows.Count} rows to Tufferman-Test-Shelving.csv");
        }

        private static void DumpVariants(System.Collections.Generic.IEnumerable<TuffermanVariant> variants)
        {
            Console.WriteLine($"Total variants: {System.Linq.Enumerable.Count(variants)}");
        }
    }
}
