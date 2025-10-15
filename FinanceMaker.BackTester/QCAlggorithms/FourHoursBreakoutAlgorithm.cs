using System;
using FinanceMaker.BackTester.QCHelpers;
using FinanceMaker.Common.Models.Finance;
using FinanceMaker.Common.Models.Pullers;
using FinanceMaker.Publisher.Orders.Trades;
using FinanceMaker.Pullers.TickerPullers;
using Microsoft.Extensions.DependencyInjection;
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Data.Market;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using YahooFinanceApi;

namespace FinanceMaker.BackTester.QCAlggorithms;

public sealed class FourHoursBreakoutAlgorithm : QCAlgorithm
{
    private Dictionary<string, int> m_TickerToPosition = new();
    private Dictionary<string, decimal> m_TickerToAvgPrice = new();
    private Dictionary<string, int> m_TickerToLoss = new();
    private Dictionary<string, int> m_TickerToWin = new();
    private List<string> m_Tickers = new();
    private Dictionary<string, float> m_TickerToMoney = new();
    private Resolution m_TestingPeriod;
    private Dictionary<string, FinanceData> m_FourHourCandleOfTheDay = new();
    private List<string> m_NeverTradeThisTicker = new();

    // Track open position and average price per ticker for P&L calculation


    public override void Initialize()
    {
        var endDate = DateTime.Now.ToUniversalTime().Date.AddDays(0);
        var startDate = endDate.AddDays(-11);
        var startDateForAlgo = new DateTime(2020, 1, 1);
        var endDateForAlgo = endDate.AddYears(-1).AddMonths(11);
        SetCash(10_000); // Starting cash for the algorithm
        SetStartDate(startDate);
        SetEndDate(endDate);
        SetSecurityInitializer(security => security.SetFeeModel(new ConstantFeeModel(0))); // $1 per trade
        FinanceData.StartDate = startDate;
        FinanceData.EndDate = endDate;      // Set Strategy Cash

        // Find more symbols here: http://quantconnect.com/data
        // Ticker from 3/10 "RGTI", "ASTS", "CRCL", "PL", "STLA", "SOUN" 
        // Ticker from 6/10 "QUBAT", "NB", "RUM", "OSCR", "RGTI", "QBTS", "RCAT", "QS", "ASPN"
        // Ticker from 7/10 "ONDS", "CLSK", "GLXY", "BTU", "NB", "PYPL", "IREN", "AMD", "TMC"
        // Ticker from 8/10 "FIG", "IREN", "JHX", "TMC", "PYPL", "POET", "RGTI", "SOFI"
        // Tciker from 10/10 "HUT", "HIVE", "WULF", "NB", "UAMY"
        // Ticker from 14/10 "ABAT", "WMT", "NVTS", "UAMY"
        var puller = StaticContainer.ServiceProvider.GetRequiredService<FourHourGapTickersPullers>();


        m_Tickers = [.. puller!.ScanTickers(TickersPullerParameters.BestBuyer, CancellationToken.None).Result];
        m_Tickers = ["RGTI", "ASTS", "CRCL", "PL", "STLA", "SOUN"];
        m_TestingPeriod = Resolution.Minute;
        SetTimeZone(TimeZones.NewYork);
        m_Tickers = m_Tickers.Distinct().ToList();
        Debug($"Tickers count: {m_Tickers.Count}");
        foreach (var ticker in m_Tickers)
        {
            var equity = AddEquity(ticker, m_TestingPeriod, extendedMarketHours: true);
            AddData<FinanceData>(equity.Symbol, m_TestingPeriod);
            m_TickerToMoney[ticker] = 0f; // Initialize realized P&L
            m_TickerToPosition[ticker] = 0;
            m_TickerToAvgPrice[ticker] = 0m;
        }
    }
    public void OnFourHoursCandle(TradeBar bar)
    {
        m_FourHourCandleOfTheDay[bar.Symbol.Value] = m_FourHourCandleOfTheDay[bar.Symbol.Value] = new FinanceData
        {
            Symbol = bar.Symbol,
            CandleStick = new EMACandleStick
            (
                bar.Time,
                (float)bar.Open,
               (float)bar.Close,
               (float)bar.High,
               (float)bar.Low, (long)bar.Volume)


        };
    }
    private void CloseLogic(FinanceData data)
    {
        float[] prices = [data.CandleStick.Open, data.CandleStick.Low, data.CandleStick.High, data.CandleStick.Close];
        var holdings = Securities[data.Symbol].Holdings;
        var avgPrice = holdings.AveragePrice;
        if (holdings.Quantity == 0) return;
        foreach (var price in prices)
        {
            decimal f = (decimal)price;
            if (f >= avgPrice * 1.6m)
            {
                Sell(data.Symbol);
            }
            else if (f <= avgPrice * 0.97m)
            {
                Sell(data.Symbol);
                m_NeverTradeThisTicker.Add(data.Symbol.Value);
            }
        }

    }
    public void OnData(FinanceData data)
    {
        var symbol = data.Symbol;
        CloseLogic(data);
        if (!m_Tickers.Contains(symbol.Value)) return;
        var numOfStartCandles = 60 * 4; // 4 hours of 1 minute candles
        var timeSpan = new TimeSpan(8, 0, 0) + TimeSpan.FromMinutes(numOfStartCandles);
        var todayCandles = History<FinanceData>(symbol, 1440, m_TestingPeriod).ToArray();
        todayCandles = todayCandles.Where(c => c.Time.Date == Time.Date)
                         .OrderBy(c => c.Time)
                         .ToArray();

        var startOfDay = todayCandles.Take(numOfStartCandles).ToList();
        if (startOfDay.Count < numOfStartCandles)
        {
            if (data.Time.TimeOfDay < timeSpan) return;
            startOfDay = todayCandles.TakeWhile(c => c.Time.TimeOfDay < timeSpan).ToList();
        }
        var restOfTheDay = todayCandles.Skip(numOfStartCandles).ToList();
        var highOfDay = startOfDay.Max(c => c.CandleStick.High);
        var lowOfDay = startOfDay.Where(c => c.CandleStick.Low > 0).Min(c => c.CandleStick.Low);
        var holdingsq = Securities[symbol].Holdings.Quantity;
        float[] prices = [data.CandleStick.Open, data.CandleStick.Low, data.CandleStick.High, data.CandleStick.Close];
        // This checks reverseall in the order
        var untilCrossedUnder = restOfTheDay.SkipWhile(candle => candle.CandleStick.Close < lowOfDay)
                                            .ToArray();

        if (untilCrossedUnder.Length == 0) return;
        var cameBack = untilCrossedUnder.SkipWhile(candle => candle.CandleStick.Close >= lowOfDay).FirstOrDefault();

        float[] keyLevels = [lowOfDay];
        var closeToKeyLevels = keyLevels.Any(level => prices.Any(price => Math.Abs(price - level) <= 0.02));
        var history = History<FinanceData>(data.Symbol, 2, m_TestingPeriod);
        if (history.Count() < 2) return;
        var secondToPrevious = history.First();
        var prevoius = history.Last();
        var isItHammer = IsItHammer(secondToPrevious.CandleStick) || prevoius.CandleStick.IsItHammer();
        // var isItReverseHammer = IsItReverseHammer(secondToPrevious.CandleStick);

        // var isItBulish = IsItBulishCandle(prevoius.CandleStick) || IsItBulishCandle(financeCandleStick);
        // var isItBerish = IsItBerishCandle(prevoius.CandleStick) || IsItBerishCandle(financeCandleStick);
        if (holdingsq == 0 &&
            !m_NeverTradeThisTicker.Contains(symbol.Value) && cameBack is not null && closeToKeyLevels && isItHammer)
        {

            Buy(symbol, data);
            return;

        }
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
    /// <summary>
    /// Executes a buy order for the given symbol.
    /// </summary>
    /// <param name="symbol">The symbol to buy.</param>
    public void Buy(Symbol symbol, FinanceData data)
    {
        Debug($"Trying to buy  {symbol.Value} at price {data.CandleStick.Close}");
        float p = 1f / m_Tickers.Count;
        SetHoldings(symbol, p);
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
            Debug($"[P&L] {symbol} CLOSE {closingQty} @ {fillPrice} (avg {prevAvgPrice}) => Realized: {realized}, Fee: {orderFee}, Time: {orderEvent.UtcTime}");

            if (realized < 0)
            {
                if (m_TickerToLoss.ContainsKey(symbol))
                    m_TickerToLoss[symbol]++;
                else
                    m_TickerToLoss[symbol] = 1;

            }
            else
            {
                if (m_TickerToWin.ContainsKey(symbol))
                    m_TickerToWin[symbol]++;
                else
                    m_TickerToWin[symbol] = 1;
            }
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
