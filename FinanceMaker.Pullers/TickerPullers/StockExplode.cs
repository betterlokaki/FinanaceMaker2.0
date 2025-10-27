using System;
using System.Collections.Concurrent;
using FinanceMaker.Common;
using FinanceMaker.Common.Models.Pullers;
using FinanceMaker.Common.Models.Pullers.Enums;
using FinanceMaker.Pullers.NewsPullers.Interfaces;
using FinanceMaker.Pullers.PricesPullers.Interfaces;

namespace FinanceMaker.Pullers.TickerPullers;

public class StockExplode : FinvizTickersPuller
{
    private readonly IPricesPuller m_Puller;
    private readonly INewsPuller m_NewsPuller;
    public StockExplode(IHttpClientFactory requestService,
                        INewsPuller newsPuller,
                        IPricesPuller puller) : base(requestService)
    {
        m_FinvizUrl = "https://finviz.com/screener.ashx?v=111&p=w&f=sh_avgvol_o1000,sh_price_o5,ta_perf_13w10o,ta_perf2_1wdown&ft=4&r=181";
        m_NewsPuller = newsPuller;
        m_Puller = puller;
    }

    public override async Task<IEnumerable<string>> ScanTickers(TickersPullerParameters scannerParams, CancellationToken cancellationToken)
    {
        var tickers = await GetTickers(m_FinvizUrl, cancellationToken);
        // List<string> tickers = ["OPEN"];
        var relevantTickers = new ConcurrentBag<string>();
        await Parallel.ForEachAsync(tickers, cancellationToken, async (ticker, ct) =>
        {
            // You can add additional filtering logic here if needed
            // For example, checking for specific financial metrics or patterns

            var weeklyCandles = (await m_Puller.GetTickerPrices(new PricesPullerParameters(ticker,
             DateTime.Now.Date.Subtract(TimeSpan.FromDays(365)),
              DateTime.Now, Period.Weekly),

              ct)).ToList();
            if (weeklyCandles.Count >= 3)
            {
                for (int i = 0; i < weeklyCandles.Count - 2; i++)
                {
                    var startPrice = weeklyCandles[i].Open;
                    var endPrice = weeklyCandles[i + 2].Close;
                    var percentageChange = (endPrice - startPrice) / startPrice * 100;

                    if (percentageChange >= 50 &&
                        weeklyCandles[i].Close > weeklyCandles[i].Open &&
                        weeklyCandles[i + 1].Close > weeklyCandles[i + 1].Open &&
                        weeklyCandles[i + 2].Close > weeklyCandles[i + 2].Open)
                    {
                        // Found a quick gap up
                        // Check if current price is near the gap up start price
                        var latestPrice = weeklyCandles.Last().Close;
                        var gapStartPrice = weeklyCandles[i].Open;

                        var priceRisk = 0.05; // 4% range

                        // if (weeklyCandles.Last().IsRed && latestPrice <= gapStartPrice * (1 + priceRisk) && latestPrice >= gapStartPrice * (1 - priceRisk))
                        if (weeklyCandles.Last().IsRed)
                        {
                            relevantTickers.Add(ticker);
                            break;

                        }
                    }

                }
            }
        });
        return [.. relevantTickers];
    }

}
