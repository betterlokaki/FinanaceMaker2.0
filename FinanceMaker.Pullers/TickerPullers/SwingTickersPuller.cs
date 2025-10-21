using System;
using System.Collections.Concurrent;
using FinanceMaker.Common;
using FinanceMaker.Common.Models.Pullers;
using FinanceMaker.Common.Models.Pullers.Enums;
using FinanceMaker.Pullers.NewsPullers.Interfaces;
using FinanceMaker.Pullers.PricesPullers.Interfaces;
using FinanceMaker.Pullers.TickerPullers.Interfaces;

namespace FinanceMaker.Pullers.TickerPullers;

public class SwingTickersPuller : FinvizTickersPuller
{
    private readonly IPricesPuller m_Puller;
    private readonly INewsPuller m_NewsPuller;
    public SwingTickersPuller(IHttpClientFactory requestService, IPricesPuller pricesPuller, INewsPuller newsPuller) : base(requestService)
    {
        m_Puller = pricesPuller;
        m_NewsPuller = newsPuller;
    }

    public override async Task<IEnumerable<string>> ScanTickers(TickersPullerParameters scannerParams, CancellationToken cancellationToken)
    {
        // var url = "https://finviz.com/screener.ashx?v=111&f=sh_avgvol_o2000,sh_curvol_o2000,sh_float_o50,sh_price_o7,ta_gap_u2&ft=4&o=-change";
        var url = "https://finviz.com/screener.ashx?v=111&f=sh_avgvol_o2000,sh_curvol_o2000,sh_float_o50,sh_price_o7,sh_relvol_o2,ta_gap_u2&ft=4&o=-change";
        var tickers = await GetTickers(url, cancellationToken);
        var relevantTickers = new ConcurrentBag<string>();
        await Parallel.ForEachAsync(tickers, cancellationToken, async (ticker, token) =>
        {
            var tickerPrices = (await m_Puller.GetTickerPrices(PricesPullerParameters.Get3DaysParams(ticker, Period.Daily),
                                                              token)).ToArray();
            var newsParams = new NewsPullerParameters(ticker, DateTime.Now.AddDays(-1), DateTime.Now);
            var news = await m_NewsPuller.PullNews(newsParams, token);
            if (!news.Any()) return;
            var lastPrice = tickerPrices.LastOrDefault();
            if (tickerPrices.Length < 2)
            {
                if (lastPrice is null) return;
                if (lastPrice.Open >= lastPrice.Close) return;
                relevantTickers.Add(ticker);
                return;
            }
            var secondToLastPrice = tickerPrices[^2];
            if (lastPrice is null) return;
            if (lastPrice.Open >= lastPrice.Close &&
                 secondToLastPrice.Open >= secondToLastPrice.Close) return;

            relevantTickers.Add(ticker);
        });
        return [.. relevantTickers];
    }
}
