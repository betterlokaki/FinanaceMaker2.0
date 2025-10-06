using System;
using System.Collections.Concurrent;
using System.Linq;
using FinanceMaker.Algorithms;
using FinanceMaker.Common;
using FinanceMaker.Common.Extensions;
using FinanceMaker.Common.Models.Finance;
using FinanceMaker.Common.Models.Finance.Enums;
using FinanceMaker.Common.Models.Ideas.IdeaOutputs;
using FinanceMaker.Common.Models.Pullers;
using FinanceMaker.Common.Models.Pullers.YahooFinance;
using FinanceMaker.Publisher.Orders.Trader.Interfaces;
using FinanceMaker.Publisher.Traders.Interfaces;
using FinanceMaker.Pullers.PricesPullers;
using FinanceMaker.Pullers.PricesPullers.Interfaces;
using FinanceMaker.Pullers.TickerPullers;
using FinanceMaker.Pullers.TickerPullers.Interfaces;
using Microsoft.Build.Framework;
using QuantConnect.Indicators;
using QuantConnect.Securities;

namespace FinanceMaker.Publisher.Traders;

/// <summary>
/// No need to do dynamic for now,
/// This trader would trade based on the range algorithm, which means if the current price is one precent around
/// of a "KeyLevel" price and the pivot is `Pivot.Low` then we should buy, and if the pivot is `Pivot.High` then we should short it.
/// And we should we should sell the stock with a risk of 3:2 risk reward ratio.
/// </summary>
public class QCTrader : ITrader
{
    private readonly ConcurrentDictionary<string, (DateTime date, KeyLevelCandleSticks result)> _dailyRangeCache = new();
    private readonly IParamtizedTickersPuller m_TickersPullers;
    private readonly RangeAlgorithmsRunner m_RangeAlgorithmsRunner;
    private readonly IPricesPuller m_PricesPuller;
    private readonly IBroker m_Broker;
    private const int NUMBER_OF_OPEN_TRADES = 10;
    private const int STARTED_MONEY = 29_500;
    private readonly Dictionary<string, float[]> m_TickerSupport = new();
    private readonly Dictionary<string, float[]> m_TickerResistance = new();
    public QCTrader(FourHourGapTickersPullers pricesPuller,
                    RangeAlgorithmsRunner rangeAlgorithmsRunner,
                    IPricesPuller mainPricesPuller,
                    IBroker broker)
    {
        m_TickersPullers = pricesPuller;
        m_RangeAlgorithmsRunner = rangeAlgorithmsRunner;
        m_Broker = broker;
        m_PricesPuller = mainPricesPuller;
        m_TickerSupport["AAPL"] = [115.19f, 128.17f, 142.92f, 156.17f, 169.88f, 186.34f, 203.14f, 217.7f, 228.7f, 245.13f];
        m_TickerResistance["AAPL"] = [119.15f, 131.3f, 144.73f, 155.66f, 168.29f, 178.48f, 192.4f, 211.81f, 228.86f, 246.27f];
        m_TickerSupport["INTC"] = [20.08f, 23.73f, 27.55f, 30.94f, 34.96f, 41.48f, 45.09f, 48.85f, 51.9f, 57.34f];
        m_TickerResistance["INTC"] = [21.09f, 25.19f, 29.73f, 33.61f, 36.7f, 42.3f, 45.91f, 49.51f, 52.55f, 58.51f];
        m_TickerSupport["PLTR"] = [8.48f, 15.89f, 23.76f, 36.68f, 67.02f, 83.26f, 111.03f, 132.53f, 154.02f, 175.73f];
        m_TickerResistance["PLTR"] = [9.04f, 16.91f, 25.19f, 38.84f, 69.67f, 87.91f, 118.8f, 138.48f, 158.88f, 180.78f];
    }

    public async Task Trade(CancellationToken cancellationToken)
    {
        var currentPosion = await m_Broker.GetClientPosition(cancellationToken);
        var tickersToTrade = await GetRelevantTickers(cancellationToken);
        tickersToTrade = tickersToTrade
            .Select(ticker => (ticker.ticker, ticker.price))
            .OrderByDescending(ticker => ticker.price)
            .ToArray();

        tickersToTrade = tickersToTrade.Where(_ => !currentPosion.OpenedPositions.Contains(_.ticker) && !currentPosion.Orders.Contains(_.ticker))
                                       .Take(NUMBER_OF_OPEN_TRADES)
                                       .ToArray();
        var buyingPower = currentPosion.BuyingPower;
        var moneyForEachTrade = STARTED_MONEY * 0.5f;
        // if (buyingPower < moneyForEachTrade && buyingPower / moneyForEachTrade < 0.6) return;
        if (buyingPower < moneyForEachTrade)
        {
            moneyForEachTrade = buyingPower * 0.78f;
        }
        if (moneyForEachTrade < 100) return;
        // if (moneyForEachTrade < STARTED_MONEY / NUMBER_OF_OPEN_TRADES) return;

        foreach (var tickerPrice in tickersToTrade)
        {
            var entryPrice = tickerPrice.price;
            var quntity = (int)(moneyForEachTrade / entryPrice);
            var ticker = tickerPrice.ticker;

            if (quntity == 0) continue;

            var stopLoss = entryPrice * 0.985f;
            var takeProfit = entryPrice * 1.02f;
            var description = $"Entry price: {entryPrice}, Stop loss: {stopLoss}, Take profit: {takeProfit}";
            var order = new EntryExitOutputIdea(description, ticker, entryPrice, takeProfit, stopLoss, quntity);

            var trade = await m_Broker.BrokerTrade(order, cancellationToken);

        }
    }

    private async Task<IEnumerable<(string ticker, float price)>> GetRelevantTickers(CancellationToken cancellationToken)
    {
        var longTickers = TickersPullerParameters.BestBuyer;
        var shortTickers = TickersPullerParameters.BestBuyer;
        // For now only long tickers, I will implement the function of short but I don't want to
        // scanTickersTwice
        // var shortTickers = TickersPullerParameters.BestSellers;
        // List<string> tickers = [
        //     //Bitcoin miners
        //     // Cars
        //         "PLTR",  "GOOGL", "AES", "CLSK","BBAI", "XPEV", "CVNA", "CAG"
        // ];
        var tickers = await m_TickersPullers.ScanTickers(longTickers, cancellationToken);

        tickers = tickers.Distinct().ToList();
        // Now we've got the stocks, we should analyze them
        var relevantTickers = new ConcurrentBag<(string ticker, float price)>();

        await Parallel.ForEachAsync(tickers, async (ticker, ca) =>
        {
            // if (m_TickerSupport.TryGetValue(ticker, out var supports))
            {
                var datas = await m_PricesPuller.GetTickerPrices(PricesPullerParameters.GetTodayParams(ticker), ca);
                var window = 60 * 4;
                var last = datas.Last();
                var list = datas.ToList(); // Convert to list to allow indexing
                var prevoius = list[^2];
                var secosecondToPreviousnd = list[^3];
                var today = datas.Where(_ => _.Time.Date ==
                                    DateTime.Today.Date)
                                    .ToArray();
                var fourHoursWindow = today.Take(window).ToArray();

                if (fourHoursWindow.Length < window)
                {
                    return;
                }
                float[] prices = [last.Open, last.Low, last.High, last.Close];

                var restOfTheDay = today.Skip(window).ToList();
                var highOfDay = fourHoursWindow.Max(c => c.High);
                var lowOfDay = fourHoursWindow.Min(c => c.Low);
                var untilCrossedUnder = restOfTheDay.SkipWhile(candle => candle.Close < lowOfDay)
                                    .ToArray();
                var cameBack = untilCrossedUnder.SkipWhile(candle => candle.Close >= lowOfDay).FirstOrDefault();
                float[] keyLevels = [lowOfDay];
                var closeToKeyLevels = keyLevels.Any(level => prices.Any(price => Math.Abs(price - level) <= 0.01));
                var isItHammer = secosecondToPreviousnd.IsItHammer() || prevoius.IsItHammer();
                // var lastPrice = prices.Last().Close;
                // var isNearSupport = supports.Any(support => Math.Abs((lastPrice - support) / support) < 0.015f);
                if (cameBack is not null && closeToKeyLevels && isItHammer)
                {
                    relevantTickers.Add((ticker, last.Low));
                }
            }
        });

        return [.. relevantTickers];
    }
}
