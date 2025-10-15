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
    private Dictionary<string, float> m_BuyKeyLevel = new();

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
        var startDate = DateTime.Now.Date.AddDays(-365);
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
        m_TestingPeriod = Resolution.Hour;
        // Define candidate tickers (Big 7, Intel, and other large-cap tech)
        m_TickerSupport["HUT"] = [4.434412494099215f, 5.335331609520974f, 6.000019860093851f, 9.393410857759672f, 11.651121227321992f, 26.129437050684174f, 31.55312228121452f, 35.24958253071404f];
        m_TickerSupport["HIVE"] = [0.14948492709099126f, 0.20000000298022869f, 0.3644088172976707f, 0.40254388347286046f, 0.5836248495487397f, 1.3744543693706586f, 1.599404418987658f, 1.9631052638238313f, 2.39914070509313f, 3.0959712742303376f, 3.724082816725016f, 4.11751559826349f, 5.311349391165448f, 10.042835561644855f, 13.368793755902185f, 14.75790297586963f];
        m_TickerSupport["WULF"] = [1.2725320532634588f, 1.4479223966705843f, 1.9103624012989713f, 2.2076914495576734f, 3.3930351995331636f, 4.6077063318555656f, 6.049619055567621f, 8.18118236675147f, 9.064913329095038f, 10.621493473816765f, 15.370066011062363f, 18.333269346346558f, 25.54161044948079f];
        m_TickerSupport["NB"] = [1.792234237117211f, 2.4332740763014136f, 3.102342978004084f];
        m_TickerSupport["UAMY"] = [0.2655707763378271f, 0.30081533709495273f, 0.36036342393614723f, 0.4183254088111043f, 0.4999256508088396f, 0.5871692344464614f, 0.6697941450696078f, 0.8981567923580487f, 1.1042203498637104f, 1.5498859034431187f, 1.8968665003756264f, 2.2274623761053447f];
        m_TickerSupport["VRT"] = [10.149300802998855f, 13.588337403929009f, 18.57926320134095f, 21.14216105186785f, 26.19084713086044f, 38.90132797038187f, 88.15004649025757f, 110.41403063141699f, 125.78898482308762f];
        m_TickerSupport["CLSK"] = [1.970763207617062f, 3.0188417096455273f, 4.176851995582706f, 6.143310939201714f, 7.634261917113984f, 9.331771753635874f, 10.51948635472803f, 13.858010777757194f, 16.15891374247574f, 23.729328946546623f, 28.378224756919245f, 34.5162189555963f];

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
        // if (!m_TickerResistance.TryGetValue(ticker, out var r)) return;
        if (data.Time.Hour < 8) return;
        int count = 0;
        float[] pricesBuy = [data.CandleStick.High, data.CandleStick.Open, data.CandleStick.Close, data.CandleStick.Low];
        // var k = keyLevels.ToList();
        // k.AddRange(r);
        // keyLevels = k.Distinct().ToArray();
        var symbol = data.Symbol;
        var holdingsq = Securities[symbol].Holdings.Quantity;
        if (holdingsq == 0)
        {
            foreach (var price in pricesBuy)
            {
                if (price <= 0)
                {
                    continue;
                }
                foreach (var value in keyLevels)
                {
                    var valueDivision = price / value;
                    if (valueDivision <= 1.015 && valueDivision >= 0.995)
                    {
                        Buy(symbol, data);
                        m_BuyKeyLevel[ticker] = value;
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
                var nextLevelIndex = m_TickerSupport[ticker].IndexOf(m_BuyKeyLevel[ticker]) + 1;
                var previousLevelIndex = nextLevelIndex - 1;
                if (nextLevelIndex >= m_TickerSupport[ticker].Length)
                {
                    if (currentPrice >= avgPrice * 1.06m)
                    {
                        Sell(data.Symbol);
                        m_BuyKeyLevel.Remove(data.Symbol.Value);
                    }

                    return;
                }
                var nextLevel = m_TickerSupport[ticker][nextLevelIndex];
                var previousLevel = m_TickerSupport[ticker][previousLevelIndex];
                if (currentPrice >= (decimal)nextLevel || currentPrice <= (decimal)(m_TickerSupport[ticker][previousLevelIndex] * 0.93f))
                {
                    Sell(data.Symbol);
                    m_BuyKeyLevel.Remove(data.Symbol.Value);
                }
            }
            else if (holdings.Quantity < 0)
            {
                if (currentPrice >= avgPrice * 1.06m || currentPrice <= (decimal)(m_BuyKeyLevel[data.Symbol.Value] * 0.96f))
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
        float p = MathF.Max(1f / m_TickerSupport.Count, 0.08f);
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
