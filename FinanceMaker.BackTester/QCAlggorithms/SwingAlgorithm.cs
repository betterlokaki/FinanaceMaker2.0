using System;
using FinanceMaker.BackTester.QCHelpers;
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;

namespace FinanceMaker.BackTester.QCAlggorithms;

/// <summary>
/// This algorithm is a bit different from the others,
/// as it is now about time to buy, and tickers more than the actucal algorithm
/// </summary>
public class SwingAlgorithm : QCAlgorithm
{
    private readonly List<string> m_Tickers = [];
    private readonly Dictionary<string, DateTime> m_TickerTriggredTimes = [];
    private Dictionary<string, int> m_TickerToPosition = [];
    private Dictionary<string, decimal> m_TickerToAvgPrice = [];
    private Dictionary<string, float> m_TickerToMoney = [];
    private Dictionary<string, int> m_TickerToLoss = new();
    private readonly HashSet<string> m_NeverTradeThisTicker = new();
    private Dictionary<string, int> m_TickerToWin = new();
    private Resolution m_TestingPeriod;

    public override void Initialize()
    {
        var endDate = DateTime.Now.ToUniversalTime().Date.AddDays(1);
        var startDate = endDate.AddDays(-2);
        var startDateForAlgo = new DateTime(2020, 1, 1);
        var endDateForAlgo = endDate.AddYears(-1).AddMonths(11);

        SetCash(20_000); // Starting cash for the algorithm
        SetStartDate(startDate);
        SetEndDate(endDate);
        SetSecurityInitializer(security => security.SetFeeModel(new ConstantFeeModel(7.5m))); // $1 per trade
        FinanceData.StartDate = startDate;
        FinanceData.EndDate = endDate;

        m_TestingPeriod = Resolution.Minute;
        SetTimeZone(TimeZones.NewYork);
        // string[] tickers = [
        //    "POET", "PL", "PONY", "ORCX", "OPEN", "ONDS", "OMER", "OLMA", "OKLO"
        // ];
        // m_Tickers.AddRange(tickers.Distinct());
        // foreach (var ticker in m_Tickers)
        // {
        //     var time = new DateTime(2025, 10, 23, 22, 53, 0, DateTimeKind.Local);
        //     var timeUtc = time.ConvertToUtc(TimeZones.Jerusalem);
        //     m_TickerTriggredTimes[ticker] = timeUtc;
        // }

        string[] tickers = [
        "FCEL", "CWVX", "CRWV", "CCCX", "BTQ", "BTU", "BTDR", "BE",
        // "AVAH", "ARTV", "ASTS", "APLD", "HUT", "HOND", "GPCR", "GLTO", "CRML"
        ];
        m_Tickers.AddRange(tickers.Distinct());
        foreach (var ticker in m_Tickers)
        {
            var time = new DateTime(2025, 10, 27, 10, 21, 0, DateTimeKind.Local);
            var timeUtc = time.ConvertToUtc(TimeZones.Jerusalem);
            m_TickerTriggredTimes[ticker] = timeUtc;
        }
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
    private void CloseLogic(FinanceData data)
    {
        float[] prices = [data.CandleStick.Open, data.CandleStick.Low, data.CandleStick.High, data.CandleStick.Close];
        var holdings = Securities[data.Symbol].Holdings;
        var avgPrice = holdings.AveragePrice;
        if (holdings.Quantity == 0) return;
        foreach (var price in prices)
        {
            decimal f = (decimal)price;
            if (price == 0) continue;
            if (f >= avgPrice * 1.07m)
            {
                Sell(data.Symbol);
                m_NeverTradeThisTicker.Add(data.Symbol.Value);

                return;
            }
            else if (f <= avgPrice * 0.98m)
            {
                Sell(data.Symbol);
                m_NeverTradeThisTicker.Add(data.Symbol.Value);

                return;
            }
            else if (Time >= EndDate.AddHours(-5))
            {
                Sell(data.Symbol);
                m_NeverTradeThisTicker.Add(data.Symbol.Value);

                return;
            }
        }

    }
    public void OnData(FinanceData data)
    {
        if (!m_TickerTriggredTimes.TryGetValue(data.Symbol.Value, out var triggerTime)) return;
        if (Time < triggerTime) return;
        CloseLogic(data);
        if (m_NeverTradeThisTicker.Contains(data.Symbol.Value)) return;
        var holdings = Securities[data.Symbol].Holdings;
        if (holdings.Quantity != 0) return;
        Buy(data.Symbol, data);
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
    /// Executes a buy order for the given symbol.
    /// </summary>
    /// <param name="symbol">The symbol to buy.</param>
    public void Short(Symbol symbol, FinanceData data)
    {
        Debug($"Trying to short  {symbol.Value} at price {data.CandleStick.Close}");
        float p = -1f / m_Tickers.Count;
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
