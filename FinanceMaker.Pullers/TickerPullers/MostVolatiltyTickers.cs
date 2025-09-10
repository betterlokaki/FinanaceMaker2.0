using FinanceMaker.Common.Extensions;
using FinanceMaker.Common.Models.Pullers;
using FinanceMaker.Pullers.TickerPullers.Interfaces;
using HtmlAgilityPack;

namespace FinanceMaker.Pullers.TickerPullers
{
    public sealed class MostVolatiltyTickers : IParamtizedTickersPuller
    {
        private readonly IHttpClientFactory m_RequestService;
        private readonly string[] m_TradingViewUrl;
        public MostVolatiltyTickers(IHttpClientFactory requestService)
        {
            m_RequestService = requestService;
            m_TradingViewUrl = [
                "https://www.tradingview.com/markets/stocks-usa/market-movers-most-volatile/?utm_source=chatgpt.com",
                "https://www.tradingview.com/markets/stocks-usa/market-movers-unusual-volume/",
                "https://www.tradingview.com/markets/stocks-usa/market-movers-gainers/",
            ];


        }
        public async Task<IEnumerable<string>> ScanTickers(TickersPullerParameters scannerParams, CancellationToken cancellationToken)
        {
            var client = m_RequestService.CreateClient();
            client.AddBrowserUserAgent();
            string[] actualTickers = [];
            foreach (var url in m_TradingViewUrl)
            {

                var result = await client.GetAsync(url, cancellationToken);
                var content = await result.Content.ReadAsStringAsync(cancellationToken);
                var doc = new HtmlDocument();
                doc.LoadHtml(content);
                var tickers = doc.DocumentNode.SelectNodes("//tr[@class=\"row-RdUXZpkv listRow\"]").Select(_ => _.Attributes["data-rowkey"].Value.Split(':')[1]).ToArray();
                if (tickers is null)
                {
                    continue;
                }
                actualTickers = actualTickers.Concat(tickers).ToArray();
            }

            if (actualTickers is null)
            {
                return [];
            }

            return actualTickers.Where(_ => !string.IsNullOrWhiteSpace(_)).ToArray();
        }
    }
}
