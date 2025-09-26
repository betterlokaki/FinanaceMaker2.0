using System;
using FinanceMaker.Algorithms;
using FinanceMaker.BackTester.QCHelpers;
using FinanceMaker.Common;
using FinanceMaker.Common.Models.Finance;
using FinanceMaker.Pullers.TickerPullers;
using Microsoft.Extensions.DependencyInjection;
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;

namespace FinanceMaker.BackTester.QCAlggorithms;

public class AiAlgorithm : QCAlgorithm
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
    /// <summary>
    /// Initializes the algorithm, loads tickers and key levels, and sets up securities.
    /// </summary>
    /// 20250926 14:05:53.658 TRACE:: Debug: Ticker: CSIQ, Realized P&L: ₪867.69
    // 20250926 14:05:53.658 TRACE:: Debug: Ticker: INTC, Realized P&L: ₪661.37
    // 20250926 14:05:53.658 TRACE:: Debug: Ticker: BABA, Realized P&L: ₪655.40
    // 20250926 14:05:53.658 TRACE:: Debug: Ticker: BE, Realized P&L: ₪633.90
    // 20250926 14:05:53.658 TRACE:: Debug: Ticker: SPWR, Realized P&L: ₪579.21
    // 20250926 14:05:53.658 TRACE:: Debug: Ticker: AAPL, Realized P&L: ₪428.75
    // 20250926 14:05:53.658 TRACE:: Debug: Ticker: PLTR, Realized P&L: ₪378.98
    // 20250926 14:05:53.658 TRACE:: Debug: Ticker: META, Realized P&L: ₪253.03
    // 20250926 14:05:53.658 TRACE:: Debug: Ticker: FSLR, Realized P&L: ₪247.20
    // 20250926 14:05:53.658 TRACE:: Debug: Ticker: GOOGL, Realized P&L: ₪141.64
    // 20250926 14:05:53.658 TRACE:: Debug: Ticker: RUN, Realized P&L: ₪85.59
    // 20250926 14:05:53.658 TRACE:: Debug: Ticker: TSLA, Realized P&L: ₪33.87
    // 20250926 14:05:53.658 TRACE:: Debug: Ticker: SEDG, Realized P&L: ₪0.00
    // 20250926 14:05:53.658 TRACE:: Debug: Ticker: NIO, Realized P&L: ₪0.00
    // 20250926 14:05:53.658 TRACE:: Debug: Ticker: MARA, Realized P&L: -₪84.96
    // 20250926 14:05:53.658 TRACE:: Debug: Ticker: ENPH, Realized P&L: -₪104.35
    // 20250926 14:05:53.658 TRACE:: Debug: Ticker: NVDA, Realized P&L: -₪682.32
    // 20250926 14:05:53.658 TRACE:: Debug: Ticker: HUT, Realized P&L: -₪840.94
    // 20250926 14:05:53.658 TRACE:: Debug: Ticker: AMD, Realized P&L: -₪1,185.31
    // 20250926 14:05:53.658 TRACE:: Debug: Ticker: AMZN, Realized P&L: -₪1,220.64
    // 20250926 14:05:53.658 TRACE:: Debug: Ticker: MSFT, Realized P&L: -₪1,409.35
    public override void Initialize()
    {
        var startDate = DateTime.Now.Date.AddDays(-5);
        var startDateForAlgo = new DateTime(2020, 1, 1);
        var endDate = DateTime.Now.AddDays(0);
        var endDateForAlgo = endDate.AddYears(-1).AddMonths(11);
        SetCash(10_000); // Starting cash for the algorithm
        SetStartDate(startDate);
        SetEndDate(endDate);
        SetSecurityInitializer(security => security.SetFeeModel(new ConstantFeeModel(2.5m))); // $1 per trade
        FinanceData.StartDate = startDate;
        FinanceData.EndDate = endDate;

        var serviceProvider = StaticContainer.ServiceProvider;
        var mainTickersPuller = serviceProvider.GetRequiredService<MainTickersPuller>();
        List<string> tickers = [];
        m_TestingPeriod = Resolution.Minute;
        // Define candidate tickers (Big 7, Intel, and other large-cap tech)
        tickers = [
            "SEDG",
        ];
        // m_TickerSupport["PLTR"] = new float[] { 8.6f, 16.01f, 24.09f, 38.06f, 68.8f, 83.78f, 107.88f, 127.54f, 150.51f, 173.4f };
        // m_TickerResistance["PLTR"] = new float[] { 8.92f, 16.64f, 25.12f, 38.95f, 71.85f, 89.67f, 114.69f, 132.78f, 155.62f, 180.04f };
        // m_TickerSupport["BABA"] = [71.34f, 82.35f, 95.08f, 111.88f, 129.81f, 156.65f, 192.96f, 214.83f, 247.32f, 281.91f];
        // m_TickerResistance["BABA"] = [72.47f, 84.0f, 97.92f, 116.29f, 136.1f, 163.1f, 200.84f, 223.57f, 253.1f, 289.59f];
        m_TickerSupport["AAPL"] = [115.19f, 128.17f, 142.92f, 156.17f, 169.88f, 186.34f, 203.14f, 217.7f, 228.7f, 245.13f];
        m_TickerResistance["AAPL"] = [119.15f, 131.3f, 144.73f, 155.66f, 168.29f, 178.48f, 192.4f, 211.81f, 228.86f, 246.27f];
        // m_TickerSupport["GOOGL"] = [85.13f, 100.12f, 115.33f, 130.24f, 141.9f, 159.68f, 171.76f, 186.55f, 199.39f, 240.75f];
        // m_TickerResistance["GOOGL"] = [86.7f, 101.67f, 116.46f, 129.12f, 139.78f, 152.75f, 166.49f, 178.49f, 197.65f, 245.92f];
        // m_TickerSupport["MSFT"] = [205.5f, 230.54f, 250.52f, 277.61f, 302.11f, 325.14f, 373.86f, 410.87f, 446.91f, 504.7f];
        // m_TickerResistance["MSFT"] = [213.11f, 243.94f, 274.55f, 296.91f, 327.66f, 370.82f, 400.34f, 421.03f, 452.01f, 510.84f];
        // m_TickerSupport["AMZN"] = [92.49f, 108.21f, 125.41f, 140.22f, 156.96f, 169.71f, 182.85f, 198.96f, 216.67f, 228.79f];
        // m_TickerResistance["AMZN"] = [96.1f, 113.03f, 131.55f, 151.33f, 164.84f, 176.62f, 188.22f, 205.39f, 222.45f, 233.57f];
        // m_TickerSupport["META"] = [121.94f, 171.09f, 213.09f, 272.64f, 319.01f, 358.81f, 487.35f, 585.94f, 686.63f, 749.78f];
        // m_TickerResistance["META"] = [127.18f, 191.67f, 267.81f, 314.59f, 354.1f, 487.14f, 539.99f, 608.86f, 705.86f, 766.6f];
        // m_TickerSupport["NVDA"] = [14.24f, 20.72f, 27.85f, 44.56f, 66.9f, 87.63f, 108.29f, 121.74f, 138.17f, 171.96f];
        // m_TickerResistance["NVDA"] = [14.27f, 20.12f, 27.82f, 46.04f, 69.76f, 90.91f, 113.7f, 127.19f, 142.36f, 175.79f];
        // m_TickerSupport["TSLA"] = [134.89f, 176.53f, 207.39f, 232.78f, 254.24f, 279.1f, 305.04f, 334.79f, 368.98f, 415.97f];
        // m_TickerResistance["TSLA"] = [141.77f, 184.24f, 215.39f, 239.45f, 261.08f, 288.16f, 317.57f, 353.1f, 395.86f, 437.85f];
        m_TickerSupport["INTC"] = [20.08f, 23.73f, 27.55f, 30.94f, 34.96f, 41.48f, 45.09f, 48.85f, 51.9f, 57.34f];
        m_TickerResistance["INTC"] = [21.09f, 25.19f, 29.73f, 33.61f, 36.7f, 42.3f, 45.91f, 49.51f, 52.55f, 58.51f];
        // m_TickerSupport["AMD"] = [63.23f, 78.78f, 90.06f, 101.05f, 109.64f, 119.18f, 133.91f, 145.68f, 159.7f, 180.06f];
        // m_TickerResistance["AMD"] = [65.74f, 80.76f, 91.68f, 103.09f, 113.8f, 125.15f, 143.41f, 160.55f, 177.42f, 203.52f];
        m_TickerSupport["PLTR"] = [8.48f, 15.89f, 23.76f, 36.68f, 67.02f, 83.26f, 111.03f, 132.53f, 154.02f, 175.73f];
        m_TickerResistance["PLTR"] = [9.04f, 16.91f, 25.19f, 38.84f, 69.67f, 87.91f, 118.8f, 138.48f, 158.88f, 180.78f];
        // m_TickerSupport["MARA"] = [3.88f, 8.15f, 11.41f, 15.54f, 19.13f, 23.59f, 29.16f, 36.24f, 47.89f, 64.35f];
        // m_TickerResistance["MARA"] = [3.35f, 7.75f, 11.47f, 16.43f, 21.1f, 27.02f, 34.25f, 41.85f, 55.07f, 77.33f];
        // m_TickerSupport["HUT"] = [5.13f, 8.83f, 11.69f, 16.45f, 21.09f, 27.04f, 36.24f, 45.59f, 58.35f, 70.39f];
        // m_TickerResistance["HUT"] = [5.44f, 9.43f, 12.47f, 17.59f, 22.23f, 27.45f, 33.7f, 42.17f, 53.09f, 70.13f];
        // m_TickerSupport["SEDG"] = [19.86f, 66.71f, 116.43f, 161.13f, 207.99f, 239.72f, 266.75f, 290.81f, 311.91f, 343.19f];
        // m_TickerResistance["SEDG"] = [21.16f, 52.95f, 78.45f, 147.71f, 213.15f, 246.17f, 272.05f, 295.76f, 320.62f, 356.63f];
        // m_TickerSupport["ENPH"] = [40.26f, 67.42f, 102.06f, 119.94f, 144.55f, 166.71f, 186.28f, 208.64f, 242.46f, 292.69f];
        // m_TickerResistance["ENPH"] = [41.92f, 69.49f, 103.91f, 121.47f, 140.82f, 164.39f, 187.88f, 215.92f, 253.49f, 307.36f];
        // m_TickerSupport["FSLR"] = [70.44f, 83.62f, 99.01f, 125.69f, 147.61f, 163.53f, 180.93f, 200.7f, 224.0f, 263.92f];
        // m_TickerResistance["FSLR"] = [74.57f, 89.68f, 107.02f, 133.82f, 157.29f, 177.5f, 197.8f, 214.88f, 238.66f, 280.51f];
        // m_TickerSupport["RUN"] = [9.36f, 14.87f, 19.26f, 24.28f, 31.31f, 42.9f, 51.33f, 58.4f, 70.01f, 82.86f];
        // m_TickerResistance["RUN"] = [8.13f, 12.4f, 18.32f, 24.96f, 33.24f, 45.3f, 53.89f, 60.73f, 72.07f, 86.94f];
        // m_TickerSupport["SPWR"] = [0.5f, 1.16f, 1.47f, 1.66f, 1.83f, 2.22f, 2.9f, 5.16f, 10.13f, 10.4f];
        // m_TickerResistance["SPWR"] = [0.61f, 1.22f, 1.54f, 1.78f, 1.98f, 2.33f, 2.75f, 3.31f, 5.11f, 10.37f];
        // m_TickerSupport["CSIQ"] = [10.76f, 15.46f, 21.33f, 27.84f, 32.84f, 36.92f, 40.21f, 44.21f, 50.95f, 57.29f];
        // m_TickerResistance["CSIQ"] = [11.29f, 15.71f, 21.27f, 26.22f, 30.82f, 35.03f, 38.73f, 42.34f, 47.82f, 59.01f];
        // m_TickerSupport["BE"] = [10.76f, 14.92f, 18.38f, 21.65f, 24.5f, 28.33f, 36.63f, 49.11f, 63.98f, 76.78f];
        // m_TickerResistance["BE"] = [11.17f, 14.94f, 18.52f, 22.15f, 25.39f, 29.53f, 39.82f, 52.92f, 68.94f, 84.5f];
        // m_TickerSupport["NIO"] = [4.54f, 7.86f, 10.96f, 17.15f, 21.2f, 29.89f, 36.99f, 42.83f, 49.47f, 57.38f];
        // m_TickerResistance["NIO"] = [4.61f, 7.51f, 10.56f, 15.73f, 21.56f, 31.11f, 38.67f, 45.04f, 52.33f, 60.81f];
        tickers = tickers.Distinct().ToList();
        var rangeAlgorithm = serviceProvider.GetService<RangeAlgorithmsRunner>();
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

        if (!m_TickerSupport.TryGetValue(ticker, out var keyLevels)) return;
        if (data.Time.Hour < 8) return;
        int count = 0;
        float[] pricesBuy = [data.CandleStick.High, data.CandleStick.Open, data.CandleStick.Close, data.CandleStick.Low];
        foreach (var price in pricesBuy)
        {
            if (price <= 0)
            {
                continue;
            }
            foreach (var value in keyLevels)
            {
                var valueDivision = price / value;
                if (valueDivision <= 1.03 && valueDivision >= 0.999999)
                {

                    var symbol = data.Symbol;
                    var holdingsq = Securities[symbol].Holdings.Quantity;
                    if (holdingsq == 0)
                    {
                        Buy(data.Symbol, data);

                        return;
                    }
                }


            }
        }
        float[] sellPrices = [data.CandleStick.Low, data.CandleStick.Open, data.CandleStick.Close, data.CandleStick.High];
        foreach (var price in sellPrices)
        {
            var holdings = Securities[data.Symbol].Holdings;
            var avgPrice = holdings.AveragePrice;
            var currentPrice = (decimal)price;

            if (price <= 0)
            {
                continue;
            }

            if (holdings.Quantity > 0)
            {
                // if (m_TickerResistance.TryGetValue(ticker, out var resistanceLevels) &&
                // Array.Exists(resistanceLevels, level => Math.Abs((float)currentPrice / level) <= 1.005 && (
                //     float)currentPrice / level >= 0.995))
                // {
                //     Sell(data.Symbol);
                //     return;
                // }

                if (currentPrice >= avgPrice * 1.02m || currentPrice <= avgPrice * 0.985m)
                {
                    Sell(data.Symbol);
                }
            }
            else if (holdings.Quantity < 0)
            {
                if (currentPrice >= avgPrice * 1.015m || currentPrice <= avgPrice * 0.975m)
                {
                    Sell(data.Symbol);
                }
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
