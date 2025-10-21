using System;
using System.Collections.Concurrent;
using System.CommandLine.Parsing;
using FinanceMaker.Common;
using FinanceMaker.Common.Extensions;
using FinanceMaker.Common.Models.Pullers;
using FinanceMaker.Common.Models.Pullers.Enums;
using FinanceMaker.Pullers.PricesPullers.Interfaces;

namespace FinanceMaker.Pullers.TickerPullers;

public class FourHourGapTickersPullers : FinvizTickersPuller
{
    private readonly IPricesPuller m_Puller;
    public FourHourGapTickersPullers(IHttpClientFactory requestService, IPricesPuller pricesPuller) : base(requestService)
    {
        m_Puller = pricesPuller;
    }

    public override async Task<IEnumerable<string>> ScanTickers(TickersPullerParameters scannerParams, CancellationToken cancellationToken)
    {
        // var url = "https://finviz.com/screener.ashx?v=111&f=sh_avgvol_o2000,sh_curvol_o2000,sh_float_o50,sh_price_o7,ta_gap_u2&ft=4&o=-change";
        // var url = "https://finviz.com/screener.ashx?v=111&f=sh_avgvol_o2000,sh_curvol_o2000,sh_float_o50,sh_price_o7,sh_relvol_o2,ta_gap_u2&ft=4&o=-change";
        var url = "https://finviz.com/screener.ashx?v=111&f=sh_curvol_o2000%2Csh_price_5to30%2Csh_relvol_o3%2Cta_change_u4&ft=4";
        var tickers = await GetTickers(url, cancellationToken);
        var relevantTickers = new ConcurrentBag<string>();
        await Parallel.ForEachAsync(tickers, cancellationToken, async (ticker, token) =>
        {
            var tickerPrices = await m_Puller.GetTickerPrices(PricesPullerParameters.Get3DaysParams(ticker, Period.Daily),
                                                              token);
            var lastPrice = tickerPrices.LastOrDefault();

            if (lastPrice is null) return;

            if (lastPrice.Open >= lastPrice.Close) return;
            relevantTickers.Add(ticker);
        });
        return [.. relevantTickers];
    }
}
