using System;
using FinanceMaker.Common;
using FinanceMaker.Common.Models.Finance;
using FinanceMaker.Common.Models.Ideas.IdeaOutputs;
using FinanceMaker.Common.Models.Pullers;
using FinanceMaker.Common.Models.Pullers.Enums;
using FinanceMaker.Publisher.Orders.Trader.Interfaces;
using FinanceMaker.Publisher.Traders.Interfaces;
using FinanceMaker.Pullers.PricesPullers.Interfaces;
using FinanceMaker.Pullers.TickerPullers;

namespace FinanceMaker.Publisher.Traders;

public class SwingTrader : ITrader
{
    private readonly EarningsCallPuller m_TickersPullers;
    private readonly IPricesPuller m_PricesPuller;
    private readonly IBroker m_Broker;
    private readonly HashSet<string> m_Tickers = [];
    private const int STARTED_MONEY = 29_500;
    public SwingTrader(EarningsCallPuller tickersPullers, IPricesPuller pricesPuller, IBroker broker)
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

            var stopLoss = entryPrice * 0.98f;
            var takeProfit = entryPrice * 1.05f;
            if (entryPrice < 0)
            {
                entryPrice = -entryPrice;
                stopLoss = entryPrice * 1.015f;
                takeProfit = entryPrice * 0.98f;
            }
            var description = $"Entry price: {entryPrice}, Stop loss: {stopLoss}, Take profit: {takeProfit}";
            var order = new EntryExitOutputIdea(description, ticker, entryPrice, takeProfit, stopLoss, quntity);

            var trade = await m_Broker.BrokerTrade(order, cancellationToken);

        }
    }
    private async Task<Dictionary<string, float>> GetPossibleTrades(CancellationToken cancellationToken)
    {
        var tickers = await m_TickersPullers.ScanTickers(cancellationToken);

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
        if (prices.Count() == 0) return 0;
        // Pre-Market strategy
        var premarketResult = PremarketStrategy(ticker, prices);
        if (premarketResult != 0) return premarketResult;
        // 5 Minutes strategy
        var fiveMinutesResult = FiveMinutesStrategy(ticker, prices);
        if (fiveMinutesResult != 0) return fiveMinutesResult;

        return 0;
    }
    private float FiveMinutesStrategy(string ticker, IEnumerable<FinanceCandleStick> prices)
    {
        var last5MinutesPrices = prices
            .Where(_ => _.Time.ToUniversalTime().TimeOfDay >= new TimeSpan(14, 30, 0))
            .OrderBy(_ => _.Time)
            .ToArray();

        if (last5MinutesPrices.Length < 5) return 0;

        var highest = last5MinutesPrices.Where(_ => _.High != 0).Max(_ => _.High);
        var lowest = last5MinutesPrices.Where(_ => _.High != 0).Min(_ => _.Low);
        var restOfTheDay = prices.Where(_ => _.Time.ToUniversalTime().TimeOfDay >= new TimeSpan(14, 35, 0)).ToArray();
        var untillCrosHigh = restOfTheDay.SkipWhile(_ => _.Close < highest).ToArray();
        var lastCandle = restOfTheDay.LastOrDefault();
        if (lastCandle is null) return 0;

        if (untillCrosHigh.Length != 0)
        {
            var highestAfterCross = untillCrosHigh.Max(_ => _.High);
            if (highestAfterCross >= highest * 1.03f &&
                lastCandle.Close >= highest * 0.98 &&
                lastCandle.Close <= highest * 1.01)
            {
                return highest * 0.99f;
            }
        }


        // var untillCrossDown = restOfTheDay.SkipWhile(_ => _.Close > lowest).ToArray();
        // if (untillCrossDown.Length == 0) return 0;
        // var lowestAfterCross = untillCrossDown.Min(_ => _.Low);
        // if (lowestAfterCross <= lowest * 0.97f &&
        //     lastCandle.Close <= lowest * 1.01 &&
        //     lastCandle.Close >= lowest * 0.99)
        // {
        //     return -lowest * 1.01f;
        // }
        return 0;
    }
    private float PremarketStrategy(string ticker, IEnumerable<FinanceCandleStick> prices)
    {
        var premarketPrices = prices
            .Where(_ => _.Time.ToUniversalTime().TimeOfDay >= new TimeSpan(9, 0, 0) &&
                _.Time.ToUniversalTime().TimeOfDay < new TimeSpan(14, 30, 0))
            .OrderBy(_ => _.Time)
            .ToArray();

        var highOfPreMarket = premarketPrices.Where(_ => _.High != 0).Max(_ => _.High);
        var lowOfPreMarket = premarketPrices.Where(_ => _.Low != 0).Min(_ => _.Low);

        var afterPremarket = prices
            .Where(_ => _.Time.ToUniversalTime().TimeOfDay >= new TimeSpan(14, 30, 0) &&
                _.Time.ToUniversalTime().TimeOfDay <= new TimeSpan(20, 0, 0))
            .OrderBy(_ => _.Time)
            .ToArray();
        var untillCrosHigh = afterPremarket.SkipWhile(_ => _.Close < highOfPreMarket).ToArray();
        var lastCandle = afterPremarket.LastOrDefault();
        if (lastCandle is null) return 0;
        if (untillCrosHigh.Length != 0)
        {
            var highestAfterCross = untillCrosHigh.Max(_ => _.High);

            if (highestAfterCross >= highOfPreMarket * 1.03f &&
             lastCandle.Close >= highOfPreMarket * 0.98 && lastCandle.Close <= highOfPreMarket * 1.01)
                return highOfPreMarket * 0.99f;
        }
        // var untillCrossDown = afterPremarket.SkipWhile(_ => _.Close > lowOfPreMarket).ToArray();
        // if (untillCrossDown.Length == 0) return 0;
        // var lowestAfterCross = untillCrossDown.Min(_ => _.Low);
        // if (lowestAfterCross <= lowOfPreMarket * 0.97f &&
        //  lastCandle.Close <= lowOfPreMarket * 1.01 && lastCandle.Close >= lowOfPreMarket * 0.99)
        //     return -lowOfPreMarket * 1.01f;

        return 0;
    }
}