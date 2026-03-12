using HtmlAgilityPack;
using System.Xml;

namespace TuffermanScraper.TitleTest
{
    internal class Program
    {
        private static readonly HttpClient Http = new HttpClient
        {
            DefaultRequestHeaders =
            {
                { "User-Agent", "ShelvingDataCollector/1.0 (internal tool)" }
            }
        };

        private const string AllShelvingUrl = "https://www.tufferman.co.uk/collections/all-shelving";

        public static async Task Main(string[] args)
        {
            Console.WriteLine("Fetching: " + AllShelvingUrl);

            var html = await Http.GetStringAsync(AllShelvingUrl);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var titleNode = doc.DocumentNode.SelectSingleNode("//title");
            Console.WriteLine("Page title: " + titleNode?.InnerText.Trim());

            Console.WriteLine("Done. If you see the title above, HTTP + HTML parsing work.");
        }
    }
}
