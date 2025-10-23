using System;
using FinanceMaker.Common;
using FinanceMaker.Common.Models.Ideas.IdeaOutputs;
using FinanceMaker.Common.Models.Pullers;
using FinanceMaker.Common.Models.Pullers.Enums;
using FinanceMaker.Publisher.Orders.Trader.Interfaces;
using FinanceMaker.Publisher.Traders.Interfaces;
using FinanceMaker.Pullers.PricesPullers.Interfaces;
using FinanceMaker.Pullers.TickerPullers;

namespace FinanceMaker.Publisher.Traders;
/// <summary>
/// This trader pulls the stocks from finviz which have a gap up in the last 4 hours
/// and then trade them based on the range algorithm. but with much much higher risk: reward ratio.
/// </summary>
public class SwingTrader : ITrader
{
    private readonly FourHourGapTickersPullers m_TickersPullers;
    private readonly IPricesPuller m_PricesPuller;
    private readonly IBroker m_Broker;
    private readonly HashSet<string> m_Tickers = [];
    private const int STARTED_MONEY = 29_500;
    public SwingTrader(FourHourGapTickersPullers tickersPullers, IPricesPuller pricesPuller, IBroker broker)
    {
        m_TickersPullers = tickersPullers;
        m_PricesPuller = pricesPuller;
        m_Broker = broker;
    }
    public async Task Trade(CancellationToken cancellationToken)
    {
        var position = await m_Broker.GetClientPosition(cancellationToken);
        var possibleTrades = await GetPossibleTrades(cancellationToken);

        possibleTrades = possibleTrades.Where(_ => !position.OpenedPositions.Contains(_.Key) && !position.Orders.Contains(_.Key))
                                        .ToDictionary();
        var buyingPower = position.BuyingPower;
        var moneyForEachTrade = STARTED_MONEY * 0.5f;
        if (buyingPower < moneyForEachTrade)
        {
            moneyForEachTrade = buyingPower * 0.78f;
        }
        if (moneyForEachTrade < 100) return;

        foreach (var tickerPrice in possibleTrades)
        {
            var entryPrice = tickerPrice.Value;
            var quntity = (int)(moneyForEachTrade / entryPrice);
            var ticker = tickerPrice.Key;

            if (quntity == 0) continue;

            var stopLoss = entryPrice * 0.985f;
            var takeProfit = entryPrice * 1.02f;
            var description = $"Entry price: {entryPrice}, Stop loss: {stopLoss}, Take profit: {takeProfit}";
            var order = new EntryExitOutputIdea(description, ticker, entryPrice, takeProfit, stopLoss, quntity);

            var trade = await m_Broker.BrokerTrade(order, cancellationToken);

        }
    }
    private async Task<Dictionary<string, float>> GetPossibleTrades(CancellationToken cancellationToken)
    {
        var tickers = await m_TickersPullers.ScanTickers(TickersPullerParameters.BestBuyer, cancellationToken);

        foreach (var ticker in tickers)
        {
            m_Tickers.Add(ticker);
        }

        var possibleTrades = new Dictionary<string, float>();

        await Parallel.ForEachAsync(m_Tickers, cancellationToken, async (ticker, token) =>
        {
            var tickerIsCool = await IsTickerCool(ticker, token);

            if (tickerIsCool == 0) return;

            possibleTrades[ticker] = tickerIsCool;
        });

        return possibleTrades;
    }


    private async Task<float> IsTickerCool(string ticker, CancellationToken cancellationToken)
    {
        var prices = await m_PricesPuller.GetTickerPrices(
            PricesPullerParameters.GetTodayParams(ticker, Period.OneMinute),
            cancellationToken);

        // Pre-Market strategy
        var premarketPrices = prices
            .Where(_ => _.Time.ToUniversalTime().TimeOfDay >= new TimeSpan(9, 0, 0) &&
                _.Time.ToUniversalTime().TimeOfDay < new TimeSpan(14, 30, 0))
            .OrderBy(_ => _.Time)
            .ToArray();

        var lastCandle = premarketPrices.LastOrDefault();
        if (lastCandle is null) return 0;
        if (lastCandle.Time.TimeOfDay <= new TimeSpan(14, 30, 0)) return 0;
        var highOfPreMarket = premarketPrices.Where(_ => _.High != 0).Max(_ => _.High);
        var lowOfPreMarket = premarketPrices.Where(_ => _.Low != 0).Min(_ => _.Low);

        var afterPremarket = prices
            .Where(_ => _.Time.ToUniversalTime().TimeOfDay >= new TimeSpan(14, 30, 0) &&
                _.Time.ToUniversalTime().TimeOfDay <= new TimeSpan(20, 0, 0))
            .OrderBy(_ => _.Time)
            .ToArray();
        var untillCrosHigh = afterPremarket.SkipWhile(_ => _.Close < highOfPreMarket).ToArray();

        if (untillCrosHigh.Length == 0) return 0;
        var highestAfterCross = untillCrosHigh.Max(_ => _.High);
        if (highestAfterCross >= highOfPreMarket * 1.03f) return highOfPreMarket * 0.99f;

        return 0;
    }

}
