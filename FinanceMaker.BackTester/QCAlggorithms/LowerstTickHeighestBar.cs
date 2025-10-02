using System;
using Accord.Math;
using FinanceMaker.Algorithms;
using FinanceMaker.BackTester.QCHelpers;
using FinanceMaker.Common;
using FinanceMaker.Common.Models.Finance;
using FinanceMaker.Pullers.TickerPullers;
using Microsoft.Extensions.DependencyInjection;
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Api;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
namespace FinanceMaker.BackTester.QCAlggorithms;

public class LowerstTickHeighestBar : QCAlgorithm
{
    private Dictionary<string, float[]> m_TickerToKeyLevels = new();
    private Dictionary<string, float> m_TickerToMoney = new();
    private Resolution m_TestingPeriod;
    // Track open position and average price per ticker for P&L calculation
    private Dictionary<string, int> m_TickerToPosition = new();
    private Dictionary<string, decimal> m_TickerToAvgPrice = new();
    private Dictionary<string, int> m_TickerToLoss = new();
    private Dictionary<string, int> m_TickerToWin = new();
    private Dictionary<string, float[]> m_TickerSupport = new();
    private Dictionary<string, float[]> m_TickerResistance = new();
    private Dictionary<string, float> m_BuyKeyLevel = new();

    /// <summary>
    public override void Initialize()
    {
        var startDate = DateTime.Now.Date.AddDays(-29);
        var endDate = DateTime.Now.AddDays(0);
        SetCash(10_000); // Starting cash for the algorithm
        SetStartDate(startDate);
        SetEndDate(endDate);
        SetSecurityInitializer(security => security.SetFeeModel(new ConstantFeeModel(0))); // $1 per trade
        FinanceData.StartDate = startDate;
        FinanceData.EndDate = endDate;
        m_TestingPeriod = Resolution.Minute;

        m_TickerSupport["PLTR"] = [7.7f, 9.59f, 12.07f, 14.64f, 16.66f, 18.8f, 21.47f, 23.72f, 26.26f, 30.75f, 36.65f, 42.88f, 57.9f, 65.46f, 71.69f, 77.08f, 82.93f, 90.0f, 97.37f, 109.18f, 118.55f, 126.24f, 133.05f, 141.1f, 151.35f, 158.84f, 170.87f, 179.23f, 185.72f];
        m_TickerSupport["AAPL"] = [109.44f, 114.65f, 119.92f, 125.1f, 130.68f, 136.35f, 141.54f, 146.03f, 150.65f, 155.45f, 159.82f, 164.04f, 168.33f, 171.99f, 175.84f, 179.76f, 183.94f, 188.6f, 192.91f, 198.01f, 203.12f, 209.94f, 216.2f, 223.58f, 228.95f, 234.15f, 240.35f, 246.39f, 254.4f];
        m_TickerSupport["MNDY"] = [86.04f, 99.11f, 110.4f, 119.82f, 128.71f, 138.02f, 148.75f, 160.55f, 172.72f, 183.18f, 193.26f, 207.71f, 217.51f, 225.95f, 234.07f, 243.05f, 253.91f, 264.16f, 274.2f, 283.99f, 293.26f, 302.04f, 312.02f, 323.83f, 343.53f, 361.49f, 378.09f, 400.79f, 425.64f];
        m_TickerSupport["INTC"] = [19.27f, 20.36f, 21.47f, 22.62f, 23.68f, 24.86f, 26.16f, 27.33f, 28.29f, 29.47f, 30.74f, 32.36f, 33.92f, 35.14f, 36.33f, 37.84f, 39.78f, 41.36f, 42.66f, 43.94f, 45.02f, 46.33f, 47.84f, 49.32f, 50.65f, 52.35f, 55.01f, 57.48f, 59.82f];
        m_TickerSupport["BABA"] = [63.25f, 70.18f, 75.24f, 79.74f, 83.47f, 87.55f, 92.38f, 98.11f, 104.98f, 110.91f, 116.49f, 122.21f, 130.96f, 140.75f, 152.67f, 160.12f, 169.98f, 186.74f, 201.52f, 212.35f, 219.92f, 228.37f, 241.99f, 248.42f, 254.23f, 264.51f, 276.65f, 287.55f, 296.21f];

        List<Task> tickersKeyLevelsLoader = [];
        foreach (var ticker in m_TickerSupport.Keys)
        {
            if (string.IsNullOrEmpty(ticker) || !m_TickerSupport.TryGetValue(ticker, out var keyLevels) || keyLevels.Length == 0) continue;
            var symbol = AddEquity(ticker, m_TestingPeriod, extendedMarketHours: true);
            AddData<FinanceData>(ticker, m_TestingPeriod);
            m_TickerToMoney[ticker] = 0f; // Initialize realized P&L
            m_TickerToPosition[ticker] = 0;
            m_TickerToAvgPrice[ticker] = 0m;
        }
    }

    /// <summary>
    /// Main data event handler. Contains entry/exit logic.
    /// </summary>
    /// <param name="data">FinanceData for a single ticker</param>
    public void OnData(FinanceData data)
    {
        FinanceData.CounterData++;
        var ticker = data.Symbol.Value;

        float[] pricesBuy = [data.CandleStick.High, data.CandleStick.Open, data.CandleStick.Close, data.CandleStick.Low];

        var symbol = data.Symbol;
        var holdingsq = Securities[symbol].Holdings.Quantity;
        var avgPrice = Securities[symbol].Holdings.AveragePrice;
        if (holdingsq != 0)
        {
            foreach (var price in pricesBuy)
            {    // Sell
                if ((float)avgPrice * 1.03 <= price || (float)avgPrice * 0.97 >= price)
                {
                    Sell(data.Symbol);
                }
            }
            return;

        }
        foreach (var price in pricesBuy)
        {
            if (price <= 0)
            {
                continue;
            }

            var p = 1;
            var oneHourCandle = History<FinanceData>(data.Symbol, p, Resolution.Hour).ToList();
            if (oneHourCandle.Count < p)
                return;
            var heighestTickLowerBar = oneHourCandle.OrderBy(_ => _.CandleStick.Low).First().CandleStick.High;
            var lowerstTickHeighestBar = oneHourCandle.OrderBy(_ => _.CandleStick.High).Last().CandleStick.Low;

            if (price >= (float)heighestTickLowerBar * 0.99 && (float)heighestTickLowerBar * 1.01 >= price)
            {
                Buy(data.Symbol, data);
            }
            else if (price <= (float)lowerstTickHeighestBar * 0.99 && (float)lowerstTickHeighestBar * 1.01 <= price)
            {

                Short(data.Symbol, data);
            }
        }
    }

    /// <summary>
    /// Executes a buy order for the given symbol.
    /// </summary>
    /// <param name="symbol">The symbol to buy.</param>
    public void Buy(Symbol symbol, FinanceData data)
    {
        Debug($"Trying to buy  {symbol.Value} at price {data.CandleStick.Close}");
        float p = 1f / m_TickerSupport.Count;
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

    public void Short(Symbol symbol, FinanceData data)
    {
        Debug($"Trying to short  {symbol.Value}");
        float p = -1f / m_TickerSupport.Count;
        SetHoldings(symbol, p);
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
