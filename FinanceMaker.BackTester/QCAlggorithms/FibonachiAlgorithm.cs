using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FinanceMaker.Algorithms;
using FinanceMaker.Algorithms.Runners;
using FinanceMaker.BackTester.QCHelpers;
using FinanceMaker.Common;
using FinanceMaker.Common.Models.Finance;
using FinanceMaker.Common.Models.Finance.Enums;
using FinanceMaker.Common.Models.Ideas.IdeaInputs;
using FinanceMaker.Pullers.TickerPullers;
using Microsoft.DotNet.Interactive.Formatting;
using Microsoft.Extensions.DependencyInjection;
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;

namespace FinanceMaker.BackTester.QCAlggorithms;

public class FibonachiAlgorithm : QCAlgorithm
{
    private Dictionary<string, float[]> m_TickerToKeyLevels = new();
    private Dictionary<string, float> m_TickerToMoney = new();
    private Resolution m_TestingPeriod;
    private Dictionary<string, RelativeStrengthIndex> m_RsiIndicators = new();
    // Track open position and average price per ticker for P&L calculation
    private Dictionary<string, int> m_TickerToPosition = new();
    private Dictionary<string, decimal> m_TickerToAvgPrice = new();
    private Dictionary<string, ExponentialMovingAverage> _ema50 = [];
    private Dictionary<string, ExponentialMovingAverage> _ema20 = [];
    /// <summary>
    /// Initializes the algorithm, loads tickers and key levels, and sets up securities.
    /// </summary>
    public override void Initialize()
    {
        var startDate = DateTime.Now.AddMonths(-1);
        var startDateForAlgo = new DateTime(2020, 1, 1);
        var endDate = DateTime.Now.AddDays(0);
        var endDateForAlgo = endDate.AddYears(-1).AddMonths(11);
        SetCash(100_00); // Starting cash for the algorithm
        SetStartDate(startDate);
        SetEndDate(endDate);
        SetSecurityInitializer(security => security.SetFeeModel(new ConstantFeeModel(2.5m))); // $1 per trade
        FinanceData.StartDate = startDate;
        FinanceData.EndDate = endDate;

        var serviceProvider = StaticContainer.ServiceProvider;
        var mainTickersPuller = serviceProvider.GetRequiredService<MainTickersPuller>();
        List<string> tickers = [];
        m_TestingPeriod = Resolution.Hour;
        // m_TickerSupport["PLTR"] = [7.72f, 9.77f, 12.53f, 15.14f, 17.32f, 19.62f, 22.11f, 24.15f, 26.42f, 30.45f, 36.43f, 43.03f, 58.77f, 65.69f, 71.68f, 77.44f, 83.15f, 90.2f, 98.32f, 109.36f, 117.73f, 124.94f, 132.75f, 141.15f, 150.64f, 158.45f, 169.88f, 177.92f, 184.36f];
        // m_TickerSupport["AAPL"] = [22.22f, 25.85f, 34.81f, 40.6f, 45.85f, 51.67f, 61.88f, 69.29f, 77.05f, 90.18f, 109.15f, 116.3f, 123.25f, 130.44f, 138.88f, 145.34f, 151.92f, 159.42f, 166.25f, 171.66f, 178.22f, 185.69f, 192.04f, 201.54f, 211.91f, 222.84f, 230.39f, 239.8f, 252.06f];
        // m_TickerSupport["MNDY"] = [86.04f, 99.11f, 110.4f, 119.82f, 128.71f, 138.02f, 148.75f, 160.55f, 172.72f, 183.18f, 193.26f, 207.71f, 217.51f, 225.95f, 234.07f, 243.05f, 253.91f, 264.16f, 274.2f, 283.99f, 293.26f, 302.04f, 312.02f, 323.83f, 343.53f, 361.49f, 378.09f, 400.79f, 425.64f];
        // m_TickerSupport["INTC"] = [19.64f, 21.03f, 22.86f, 24.35f, 25.77f, 27.8f, 29.11f, 30.21f, 31.35f, 32.86f, 34.13f, 35.59f, 37.15f, 38.61f, 40.17f, 41.51f, 42.83f, 44.03f, 45.02f, 46.03f, 47.09f, 48.41f, 49.69f, 50.85f, 51.92f, 52.98f, 54.68f, 57.18f, 59.75f];
        // m_TickerSupport["BABA"] = [62.45f, 69.66f, 74.62f, 79.39f, 83.5f, 87.97f, 93.73f, 98.99f, 105.45f, 112.24f, 118.27f, 125.1f, 133.85f, 142.06f, 148.98f, 156.99f, 162.78f, 168.13f, 173.63f, 179.95f, 188.92f, 198.85f, 207.24f, 216.33f, 227.28f, 240.48f, 251.11f, 263.71f, 286.8f];

        // Define candidate tickers (Big 7, Intel, and other large-cap tech)
        tickers = [
        //Bitcoin miners
        // "HUT", 
        // Cars
        // "TSLA", 
        // Best buyers
        //    "AES", "NKE"
        //    "TSM", "QS", "LAC", "PLUG", "MU"
        "PLTR", "AAPL", "BABA", "MNDY", "INTC"
        ];
        var rangeAlgorithm = serviceProvider.GetService<FibonachiRunner>();
        List<Task> tickersKeyLevelsLoader = [];

        foreach (var ticker in tickers)
        {
            var tickerKeyLevelsLoader = Task.Run(async () =>
            {
                var actualTicker = ticker;
                var range = await rangeAlgorithm!.Run(new RangeAlgorithmInput(new PricesPullerParameters(
                    actualTicker,
                    startDateForAlgo,
                    endDateForAlgo,
                    Common.Models.Pullers.Enums.Period.Daily), Algorithm.KeyLevels), CancellationToken.None);
                if (range is not KeyLevelCandleSticks candleSticks) return;
                m_TickerToKeyLevels[actualTicker] = candleSticks.KeyLevels;
            });
            tickersKeyLevelsLoader.Add(tickerKeyLevelsLoader);
        }
        Task.WhenAll(tickersKeyLevelsLoader).Wait();

        // m_TickerToKeyLevels["PLTR"] = [7.7f, 9.59f, 12.07f, 14.64f, 16.66f, 18.8f, 21.47f, 23.72f, 26.26f, 30.75f, 36.65f, 42.88f, 57.9f, 65.46f, 71.69f, 77.08f, 82.93f, 90.0f, 97.37f, 109.18f, 118.55f, 126.24f, 133.05f, 141.1f, 151.35f, 158.84f, 170.87f, 179.23f, 185.72f];
        // m_TickerToKeyLevels["AAPL"] = [109.44f, 114.65f, 119.92f, 125.1f, 130.68f, 136.35f, 141.54f, 146.03f, 150.65f, 155.45f, 159.82f, 164.04f, 168.33f, 171.99f, 175.84f, 179.76f, 183.94f, 188.6f, 192.91f, 198.01f, 203.12f, 209.94f, 216.2f, 223.58f, 228.95f, 234.15f, 240.35f, 246.39f, 254.4f];
        // m_TickerToKeyLevels["MNDY"] = [86.04f, 99.11f, 110.4f, 119.82f, 128.71f, 138.02f, 148.75f, 160.55f, 172.72f, 183.18f, 193.26f, 207.71f, 217.51f, 225.95f, 234.07f, 243.05f, 253.91f, 264.16f, 274.2f, 283.99f, 293.26f, 302.04f, 312.02f, 323.83f, 343.53f, 361.49f, 378.09f, 400.79f, 425.64f];
        // m_TickerToKeyLevels["INTC"] = [19.27f, 20.36f, 21.47f, 22.62f, 23.68f, 24.86f, 26.16f, 27.33f, 28.29f, 29.47f, 30.74f, 32.36f, 33.92f, 35.14f, 36.33f, 37.84f, 39.78f, 41.36f, 42.66f, 43.94f, 45.02f, 46.33f, 47.84f, 49.32f, 50.65f, 52.35f, 55.01f, 57.48f, 59.82f];
        // m_TickerToKeyLevels["BABA"] = [63.25f, 70.18f, 75.24f, 79.74f, 83.47f, 87.55f, 92.38f, 98.11f, 104.98f, 110.91f, 116.49f, 122.21f, 130.96f, 140.75f, 152.67f, 160.12f, 169.98f, 186.74f, 201.52f, 212.35f, 219.92f, 228.37f, 241.99f, 248.42f, 254.23f, 264.51f, 276.65f, 287.55f, 296.21f];
        var actualTickers = m_TickerToKeyLevels.OrderByDescending(_ => _.Value?.Length ?? 0)
                                               .Select(_ => _.Key)
                                               .ToArray();

        foreach (var ticker in actualTickers)
        {
            if (string.IsNullOrEmpty(ticker) || !m_TickerToKeyLevels.TryGetValue(ticker, out var keyLevels) || keyLevels.Length == 0) continue;
            var symbol = AddEquity(ticker, m_TestingPeriod, extendedMarketHours: true);
            AddData<FinanceData>(ticker, m_TestingPeriod);
            m_TickerToMoney[ticker] = 0f; // Initialize realized P&L
            m_TickerToPosition[ticker] = 0;
            m_TickerToAvgPrice[ticker] = 0m;
            _ema50[symbol.Symbol.Value] = EMA(symbol.Symbol, 50, m_TestingPeriod);
            _ema20[symbol.Symbol.Value] = EMA(symbol.Symbol, 20, m_TestingPeriod);
            // This makes Lean update the indicators when TradeBars arrive
            RegisterIndicator(symbol.Symbol, _ema50[symbol.Symbol.Value], m_TestingPeriod);
            RegisterIndicator(symbol.Symbol, _ema20[symbol.Symbol.Value], m_TestingPeriod);
        }
    }
    public void OnData(FinanceData data)
    {

        var symbol = data.Symbol.Value;
        var financeCandleStick = data.CandleStick;
        _ema50[symbol].Update(new TradeBar(data.Time, data.Symbol, (decimal)financeCandleStick.Open, (decimal)financeCandleStick.High, (decimal)financeCandleStick.Close, (decimal)financeCandleStick.Low, (decimal)financeCandleStick.Volume, TimeSpan.FromHours(1)));
        _ema20[symbol].Update(new TradeBar(data.Time, data.Symbol, (decimal)financeCandleStick.Open, (decimal)financeCandleStick.High, (decimal)financeCandleStick.Close, (decimal)financeCandleStick.Low, (decimal)financeCandleStick.Volume, TimeSpan.FromHours(1)));
        if (!_ema50[symbol].IsReady || !_ema20[symbol].IsReady) return;

        float[] prices = [data.CandleStick.Open, data.CandleStick.Close, data.CandleStick.Low, data.CandleStick.High];

        var holdings = Securities[data.Symbol].Holdings;
        var avgPrice = holdings.AveragePrice;
        var currentPrice = (decimal)data.CandleStick.Close;

        if (holdings.Quantity != 0)
        {
            if (Math.Abs(avgPrice - currentPrice) >= 0.07m)
            {
                Sell(data.Symbol);

            }
        }
        var emaShort = _ema20[symbol];
        var emaLong = _ema50[symbol];
        var downTrend = emaShort < emaLong;
        var upTrend = emaShort > emaLong;
        var keyLevels = m_TickerToKeyLevels[symbol];
        var closeToKeyLevels = keyLevels.Any(level => prices.Any(price => Math.Abs(price - level) >= 0.01));
        var history = History<FinanceData>(data.Symbol, 2, m_TestingPeriod);
        if (history.Count() < 2) return;
        var secondToPrevious = history.First();
        var prevoius = history.Last();
        var isItHammer = IsItHammer(secondToPrevious.CandleStick);
        var isItReverseHammer = IsItReverseHammer(secondToPrevious.CandleStick);

        var isItBulish = IsItBulishCandle(prevoius.CandleStick) || IsItBulishCandle(financeCandleStick);
        var isItBerish = IsItBerishCandle(prevoius.CandleStick) || IsItBerishCandle(financeCandleStick);

        // if (upTrend && closeToKeyLevels)
        // {
        //     Short(data.Symbol);

        // }
        if (closeToKeyLevels && isItBulish && isItHammer)
        {
            Buy(data.Symbol);
        }

    }
    private bool IsItBulishCandle(FinanceCandleStick candle)
    {
        var body = candle.Close - candle.Open;
        var range = candle.High - candle.Low;
        if (range == 0) return false;
        // Big body (>1% of range) and close near high, open near low
        return candle.Close > candle.Open;
    }

    private bool IsItBerishCandle(FinanceCandleStick candle)
    {
        var body = candle.Open - candle.Close;
        var range = candle.High - candle.Low;
        if (range == 0) return false;
        // Big body (>1% of range) and close near low, open near high
        return candle.Open > candle.Close;
    }
    private bool IsItHammer(FinanceCandleStick candle)
    {
        var body = Math.Abs(candle.Close - candle.Open);
        var lowerShadow = candle.Open > candle.Close ? candle.Close - candle.Low : candle.Open - candle.Low;
        var upperShadow = candle.High - Math.Max(candle.Close, candle.Open);

        // Hammer criteria: small body, long lower shadow, little or no upper shadow
        if (body == 0) return false; // Avoid doji
        bool isHammer = lowerShadow >= 2 * body && upperShadow <= body;
        return isHammer;
    }

    private bool IsItReverseHammer(FinanceCandleStick candle)
    {
        var body = Math.Abs(candle.Close - candle.Open);
        var lowerShadow = Math.Min(candle.Open, candle.Close) - candle.Low;
        var upperShadow = candle.High - Math.Max(candle.Close, candle.Open);

        // Reverse Hammer: small body, long upper shadow, little or no lower shadow
        if (body == 0) return false; // avoid doji
        return upperShadow >= 2 * body && lowerShadow <= body;
    }
    /// <summary>
    /// Executes a buy order for the given symbol.
    /// </summary>
    /// <param name="symbol">The symbol to buy.</param>
    public void Buy(Symbol symbol)
    {
        Debug("Trying to buy " + symbol.Value);
        float p = 1f / m_TickerToKeyLevels.Count;
        SetHoldings(symbol, 0.5);
    }

    /// <summary>
    /// Executes a sell (liquidate) order for the given symbol.
    /// </summary>
    /// <param name="symbol">The symbol to sell.</param>
    public void Sell(Symbol symbol)
    {
        Liquidate(symbol);
    }

    /// <summary>
    /// Executes a short order for the given symbol.
    /// </summary>
    /// <param name="symbol">The symbol to short.</param>
    public void Short(Symbol symbol)
    {
        float p = -1f / m_TickerToKeyLevels.Count;
        SetHoldings(symbol, -0.5);
    }

    /// <summary>
    /// Handles order events to track realized P&L per ticker.
    /// </summary>
    /// <param name="orderEvent">The order event.</param>
    public override void OnOrderEvent(OrderEvent orderEvent)
    {
        base.OnOrderEvent(orderEvent);
        if (orderEvent.Status != OrderStatus.Filled && orderEvent.Status != OrderStatus.PartiallyFilled)
            return;
        var symbol = orderEvent.Symbol.Value;
        if (!m_TickerToMoney.ContainsKey(symbol))
            m_TickerToMoney[symbol] = 0f;
        if (!m_TickerToPosition.ContainsKey(symbol))
            m_TickerToPosition[symbol] = 0;
        if (!m_TickerToAvgPrice.ContainsKey(symbol))
            m_TickerToAvgPrice[symbol] = 0m;

        int fillQty = (int)orderEvent.FillQuantity;
        decimal fillPrice = orderEvent.FillPrice;
        decimal orderFee = orderEvent.OrderFee?.Value.Amount ?? 0m;
        int prevPosition = m_TickerToPosition[symbol];
        decimal prevAvgPrice = m_TickerToAvgPrice[symbol];
        int newPosition = prevPosition + fillQty;

        // If closing part or all of a position (direction change or reduce)
        if (prevPosition != 0 && Math.Sign(prevPosition) != Math.Sign(fillQty))
        {
            // Amount being closed is the smaller of abs(fillQty) and abs(prevPosition)
            int closingQty = Math.Abs(Math.Min(Math.Abs(fillQty), Math.Abs(prevPosition)) * Math.Sign(fillQty));
            decimal realized = 0m;
            if (prevPosition > 0) // Closing a long
                realized = closingQty * (fillPrice - prevAvgPrice) - orderFee;
            else // Closing a short
                realized = closingQty * (prevAvgPrice - fillPrice) - orderFee;
            m_TickerToMoney[symbol] += (float)realized;
            Debug($"[P&L] {symbol} CLOSE {closingQty} @ {fillPrice} (avg {prevAvgPrice}) => Realized: {realized}, Fee: {orderFee}");
        }

        // Update position and average price
        int totalQty = prevPosition + fillQty;
        if (totalQty == 0)
        {
            m_TickerToAvgPrice[symbol] = 0m;
            m_TickerToPosition[symbol] = 0;
        }
        else if (Math.Sign(fillQty) == Math.Sign(totalQty))
        {
            // Increasing position in same direction, update average price
            m_TickerToAvgPrice[symbol] = (prevAvgPrice * prevPosition + fillPrice * fillQty) / totalQty;
            m_TickerToPosition[symbol] = totalQty;
        }
        else
        {
            // Flipping position: set avg price to fill price for new position
            m_TickerToAvgPrice[symbol] = fillPrice;
            m_TickerToPosition[symbol] = totalQty;
        }
    }

    /// <summary>
    /// Called at the end of the algorithm. Prints per-ticker realized P&L.
    /// </summary>
    public override void OnEndOfAlgorithm()
    {
        Debug("--- Per-Ticker Realized P&L ---");
        m_TickerToMoney = m_TickerToMoney.OrderByDescending(kvp => kvp.Value)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        foreach (var kvp in m_TickerToMoney)
        {
            Debug($"Ticker: {kvp.Key}, Realized P&L: {kvp.Value:C2}");
        }
    }

}
