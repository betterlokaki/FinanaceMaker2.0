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
using FinanceMaker.Publisher.Orders.Trader.Interfaces;
using FinanceMaker.Publisher.Traders.Interfaces;
using FinanceMaker.Pullers.PricesPullers;
using FinanceMaker.Pullers.PricesPullers.Interfaces;
using FinanceMaker.Pullers.TickerPullers;
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
    private readonly MainTickersPuller m_TickersPullers;
    private readonly RangeAlgorithmsRunner m_RangeAlgorithmsRunner;
    private readonly IPricesPuller m_PricesPuller;
    private readonly IBroker m_Broker;
    private const int NUMBER_OF_OPEN_TRADES = 3;
    private const int STARTED_MONEY = 10_500;
    public QCTrader(MainTickersPuller pricesPuller,
                    RangeAlgorithmsRunner rangeAlgorithmsRunner,
                    IPricesPuller mainPricesPuller,
                    IBroker broker)
    {
        m_TickersPullers = pricesPuller;
        m_RangeAlgorithmsRunner = rangeAlgorithmsRunner;
        m_Broker = broker;
        m_PricesPuller = mainPricesPuller;
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
        if (!tickersToTrade.Any())
        {
            tickersToTrade = [("NIO", 6.00f)];
        }
        var buyingPower = currentPosion.BuyingPower;
        var moneyForEachTrade = STARTED_MONEY * 0.5f;
        if (buyingPower < moneyForEachTrade && buyingPower / moneyForEachTrade < 0.6) return;
        if (buyingPower < moneyForEachTrade)
        {
            moneyForEachTrade = buyingPower * 0.78f;
        }
        // if (moneyForEachTrade < STARTED_MONEY / NUMBER_OF_OPEN_TRADES) return;

        foreach (var tickerPrice in tickersToTrade)
        {
            var entryPrice = tickerPrice.price;
            var quntity = (int)(moneyForEachTrade / entryPrice);
            var ticker = tickerPrice.ticker;

            if (quntity == 0) continue;

            var stopLoss = MathF.Round(entryPrice * 0.985f, 2);
            var takeProfit = MathF.Round(entryPrice * 1.02f, 2);
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
        List<string> tickers = [
            //Bitcoin miners
            "HUT",
            // Cars
            "PLTR", "AAPL", "GOOGL"
        ];

        tickers = tickers.Distinct().ToList();
        // Now we've got the stocks, we should analyze them
        var relevantTickers = new ConcurrentBag<(string ticker, float price)>();

        await Parallel.ForEachAsync(tickers, async (ticker, ca) =>
        {
            {
                // Cache the daily range result to avoid recomputation within the same day
                if (_dailyRangeCache.TryGetValue(ticker, out var mip) && mip.date < DateTime.Today)
                {
                    // Remove not relevant to not blow tthe machine
                    _dailyRangeCache.Remove(ticker, out var p);
                }
                KeyLevelCandleSticks candleSticks;

                if (_dailyRangeCache.TryGetValue(ticker, out var cached))
                {
                    candleSticks = cached.result;
                }
                else
                {
                    var range = await m_RangeAlgorithmsRunner.Run<EMACandleStick>(
                        new RangeAlgorithmInput(new PricesPullerParameters(
                            ticker,
                            DateTime.Now.AddYears(-5),
                            DateTime.Now,
                            Common.Models.Pullers.Enums.Period.Daily), Algorithm.KeyLevels), cancellationToken);

                    if (range is not KeyLevelCandleSticks ks || !ks.Any())
                        return;

                    candleSticks = ks;
                    _dailyRangeCache[ticker] = (DateTime.Today, ks);
                }

                var interdayCandles = await m_RangeAlgorithmsRunner.Run<EMACandleStick>(
                    new RangeAlgorithmInput(PricesPullerParameters.GetTodayParams(ticker), Algorithm.KeyLevels),
                                                                                        cancellationToken);
                int numberOfCandles = 90;
                if (interdayCandles is not KeyLevelCandleSticks interdayCandleSticks ||
                    !interdayCandleSticks.Any() ||
                    interdayCandleSticks.Count < numberOfCandles)
                    return;
                var keyLevels = candleSticks.KeyLevels.OrderByDescending(_ => _).Skip(1);
                foreach (var keylevel in keyLevels)
                {
                    var lastCandleStick = interdayCandleSticks.Last();

                    var recentCandles = interdayCandleSticks[^numberOfCandles..]; // last 4 candles
                                                                                  // If we want to use the average value of the last 2 candles, we can uncomment the next line
                                                                                  // This is not the best way to do it, but it will work for now
                                                                                  // var averageValue = recentCandles[^2..].Average(candle => candle.Close);  
                                                                                  // var averageValue = recentCandles[..2].Average(candle => candle.Close);

                    var valueDivision = Math.Abs(lastCandleStick.Close) / keylevel;

                    bool nearKeyLevel = valueDivision <= 1.005 && valueDivision >= 0.995;
                    var previousHistory = recentCandles;

                    if (previousHistory is not null && previousHistory.Any() && nearKeyLevel)
                    {
                        var spyResult2 = previousHistory.Select(_ => _).ToList();
                        bool isBullishReversal = spyResult2.Take(spyResult2.Count / 2).All(c => c.Close < c.Open) &&
                      spyResult2.Skip(spyResult2.Count / 2).All(c => c.Close > c.Open);

                        bool isBearishReversal = spyResult2.Take(spyResult2.Count / 2).All(c => c.Close > c.Open) &&
                                                  spyResult2.Skip(spyResult2.Count / 2).All(c => c.Close < c.Open);

                        if (isBearishReversal) return;

                        {
                            relevantTickers.Add((ticker, lastCandleStick.Close));
                            break;
                        }
                    }

                }
            }
        });

        return [.. relevantTickers];
    }
}
