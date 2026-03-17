using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using TuffermanScraper.Complete;
using TuffermanScraper.Complete.Models;

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

        var scraper = new CompleteScraper(http);
        var allRows = new List<ShelvingRow>();

        foreach (var url in TestUrls)
        {
            try
            {
                var rows = await scraper.ScrapeProductAsync(url);

                foreach (var r in rows)
                {
                    Console.WriteLine(
                        $"{r.Supplier} | H={r.Height} W={r.Width} D={r.Depth} Load={r.MaxLoadPerLevel} " +
                        $"| Was={r.Was} Now={r.Now}");
                }

                allRows.AddRange(rows);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [ERROR] Failed to scrape {url}: {ex.Message}");
            }

            // Be polite to the server
            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        const string outputPath = "Tufferman-Complete-Shelving.csv";

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
        };

        using var writer = new StreamWriter(outputPath, false, System.Text.Encoding.UTF8);
        using var csv = new CsvWriter(writer, config);
        csv.Context.RegisterClassMap<ShelvingRowMap>();
        csv.WriteRecords(allRows);

        Console.WriteLine($"\nWrote {allRows.Count} rows to {outputPath}");
    }
}
