using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using TuffermanScraper.ExtractTest.Models;
using TuffermanScraper.ExtractTest.Transformers;

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
            Http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "ShelvingDataCollector/1.0 (+internal use; harryc@slingsby.com)");

            var scraper = new TuffermanScraper(Http);

            // --- OPTION A: test on a single product page ---
            var testUrls = new[]
            {
                new Uri("https://www.tufferman.co.uk/products/3x-vrs-heavy-duty-shelving-1800mm-high-200-280kg-grey"),
                new Uri("https://www.tufferman.co.uk/products/1x-vrs-shelving-unit-1800mm-high-grey-with-wham-diy-recycled-plastic-storage-boxes"),
                new Uri("https://www.tufferman.co.uk/products/1x-eclipse-chrome-wire-shelving-extension-bay-1625mm-high-300kg"),
                new Uri("https://www.tufferman.co.uk/products/twin-slot-wall-mounted-shelving-1000mm-wide-melamine-black")
            };

            var allTestVariants = new List<TuffermanVariant>();

            foreach (var u in testUrls)
            {
                var vs = await scraper.ScrapeProductPageAsync(u);
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
