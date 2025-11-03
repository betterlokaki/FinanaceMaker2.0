using System;
using System.IO.Compression;
using System.Security.Policy;
using System.Text;
using Accord;
using FinanceMaker.Common.Extensions;
using FinanceMaker.Pullers.TickerPullers.Interfaces;
using HtmlAgilityPack;

namespace FinanceMaker.Pullers.TickerPullers;

public class EarningsCallPuller : ITickerPuller
{
    private const string m_YahooUrl = "https://finance.yahoo.com/calendar/earnings?from={0}&to={1}&day={2}";
    private readonly IHttpClientFactory m_RequestService;
    public EarningsCallPuller(IHttpClientFactory requestService)
    {
        m_RequestService = requestService;
    }
    public async Task<IEnumerable<string>> ScanTickers(CancellationToken cancellationToken)
    {
        var today = DateTime.Now;
        var fromDate = today.ToString("yyyy-MM-dd");
        var toDate = today.ToString("yyyy-MM-dd");
        var day = today.ToString("yyyy-MM-dd");
        var url = string.Format(m_YahooUrl, fromDate, toDate, day);

        var httpClient = m_RequestService.CreateClient();
        httpClient.AddBrowserUserAgent();
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127 Safari/537.36");
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://finance.yahoo.com/");
        var finvizResult = await httpClient.GetAsync(url, cancellationToken);

        if (!finvizResult.IsSuccessStatusCode)
        {
            return [];
        }
        var enc = finvizResult.Content.Headers.ContentEncoding; // inspect in debugger or log

        // preferred: read as string (handler will auto-decompress)

        using var contentStream = await finvizResult.Content.ReadAsStreamAsync(cancellationToken);
        Stream decompressed = contentStream;
        decompressed = new System.IO.Compression.GZipStream(contentStream, CompressionMode.Decompress);

        using var sr = new StreamReader(decompressed, Encoding.UTF8);
        var yahooManual = await sr.ReadToEndAsync(cancellationToken);
        // var yahoo = await finvizResult.Content.ReadAsStringAsync(cancellationToken);
        // var htmlWeb = new HtmlWeb();
        var document = new HtmlDocument();
        document.LoadHtml(yahooManual);

        var node = document.DocumentNode;
        var xpath = "//tbody//td[@data-testid-cell=\"ticker\"]";
        var tickers = node.SelectNodes(xpath)
                          .Select(_ => _.InnerText.Trim())
                          .Where(_ => _.Length <= 4)
                          .ToList();

        return tickers;
    }
}
