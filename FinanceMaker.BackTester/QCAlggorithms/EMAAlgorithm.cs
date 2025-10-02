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
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
namespace FinanceMaker.BackTester.QCAlggorithms;

public class EMAAlgorithm : QCAlgorithm
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
    private Dictionary<string, ExponentialMovingAverage> _ema50 = [];
    private Dictionary<string, ExponentialMovingAverage> _ema200 = [];
    public override void Initialize()
    {
        var startDate = DateTime.Now.Date.AddDays(-29);
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
            // "SEDG", "SPWR", "FSLR", // Solar
            // "AAPL", "MSFT", "GOOGL", "AMZN", "META", "NVDA", // Big 6 Tech
            "BABA"
            // "PLTR", // Big Data
            // "BABA", // E-commerce/China Tech
            // "CSIQ", "ENPH", // Solar
            // "MARA", "HUT", // Bitcoin Miners
            // "BE", // Big 7 Alternative Energy
            // "RUN" // Renewable Energy
        ];
        m_TickerSupport["AAPL"] = [115.19f, 128.17f, 142.92f, 156.17f, 169.88f, 186.34f, 203.14f, 217.7f, 228.7f, 245.13f];
        m_TickerResistance["AAPL"] = [119.15f, 131.3f, 144.73f, 155.66f, 168.29f, 178.48f, 192.4f, 211.81f, 228.86f, 246.27f];
        m_TickerSupport["INTC"] = [20.08f, 23.73f, 27.55f, 30.94f, 34.96f, 41.48f, 45.09f, 48.85f, 51.9f, 57.34f];
        m_TickerResistance["INTC"] = [21.09f, 25.19f, 29.73f, 33.61f, 36.7f, 42.3f, 45.91f, 49.51f, 52.55f, 58.51f];
        m_TickerSupport["PLTR"] = [8.48f, 15.89f, 23.76f, 36.68f, 67.02f, 83.26f, 111.03f, 132.53f, 154.02f, 175.73f];
        m_TickerResistance["PLTR"] = [9.04f, 16.91f, 25.19f, 38.84f, 69.67f, 87.91f, 118.8f, 138.48f, 158.88f, 180.78f];
        m_TickerSupport["HUT"] = [5.13f, 8.83f, 11.69f, 16.45f, 21.09f, 27.04f, 36.24f, 45.59f, 58.35f, 70.39f];
        m_TickerResistance["HUT"] = [5.44f, 9.43f, 12.47f, 17.59f, 22.23f, 27.45f, 33.7f, 42.17f, 53.09f, 70.13f];

        var rangeAlgorithm = serviceProvider.GetService<RangeAlgorithmsRunner>();
        List<Task> tickersKeyLevelsLoader = [];
        foreach (var ticker in tickers)
        {
            var symbol = AddEquity(ticker, m_TestingPeriod, extendedMarketHours: true);
            AddData<FinanceData>(ticker, m_TestingPeriod);
            _ema50[symbol.Symbol.Value] = EMA(symbol.Symbol, 50, m_TestingPeriod);
            _ema200[symbol.Symbol.Value] = EMA(symbol.Symbol, 200, m_TestingPeriod);
            // This makes Lean update the indicators when TradeBars arrive
            RegisterIndicator(symbol.Symbol, _ema50[symbol.Symbol.Value], m_TestingPeriod);
            RegisterIndicator(symbol.Symbol, _ema200[symbol.Symbol.Value], m_TestingPeriod);
        }
    }

    /// <summary>
    /// Main data event handler. Contains entry/exit logic.
    /// </summary>
    /// <param name="data">FinanceData for a single ticker</param>
    public void OnData(FinanceData data)
    {
        var symbol = data.Symbol.Value;
        var financeCandleStick = data.CandleStick;
        _ema50[symbol].Update(new TradeBar(data.Time, data.Symbol, (decimal)financeCandleStick.Open, (decimal)financeCandleStick.High, (decimal)financeCandleStick.Close, (decimal)financeCandleStick.Low, (decimal)financeCandleStick.Volume, TimeSpan.FromHours(1)));
        _ema200[symbol].Update(new TradeBar(data.Time, data.Symbol, (decimal)financeCandleStick.Open, (decimal)financeCandleStick.High, (decimal)financeCandleStick.Close, (decimal)financeCandleStick.Low, (decimal)financeCandleStick.Volume, TimeSpan.FromHours(1)));
        if (!_ema50[symbol].IsReady || !_ema200[symbol].IsReady) return;

        // Entry logic: Price above both EMAs and EMAs are aligned (50 > 200)
        var holdings = Securities[data.Symbol].Holdings;
        var avgPrice = holdings.AveragePrice;
        var currentPrice = (decimal)data.CandleStick.Close;
        if (data.CandleStick.Close > _ema50[symbol] && _ema50[symbol] > _ema200[symbol] && m_TickerSupport[symbol].Any(support => currentPrice <= (decimal)support * 1.001m && currentPrice >= (decimal)support * 0.999m))
        {
            if (holdings.Quantity == 0)
            {
                Buy(data.Symbol, data);
            }
        }
        // Exit logic: Price below either EMA or EMAs are misaligned
        else if (
                (currentPrice >= avgPrice * 1.03m))
        {

            if (holdings.Quantity > 0)
            {
                Sell(data.Symbol);
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

