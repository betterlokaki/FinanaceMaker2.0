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

public class RangePlusAlgorithm : QCAlgorithm
{
    private Dictionary<string, float[]> m_TickerToKeyLevels = new();
    private Dictionary<string, float> m_TickerToMoney = new();
    private Resolution m_TestingPeriod;
    private List<string> m_ProblematicTickers = [];
    // Track open position and average price per ticker for P&L calculation
    private Dictionary<string, int> m_TickerToPosition = new();
    private Dictionary<string, decimal> m_TickerToAvgPrice = new();
    private Dictionary<string, int> m_TickerToLoss = new();
    private Dictionary<string, int> m_TickerToWin = new();
    private IEnumerable<FinanceCandleStick> m_SpyData = [];
    /// <summary>
    /// Initializes the algorithm, loads tickers and key levels, and sets up securities.
    /// </summary>
    public override void Initialize()
    {
        var startDate = DateTime.Now.Date.AddDays(-29);
        var startDateForAlgo = new DateTime(2020, 1, 1);
        var endDate = DateTime.Now.AddDays(0);
        var endDateForAlgo = endDate.AddYears(-1).AddMonths(11);
        SetCash(10_700); // Starting cash for the algorithm
        SetStartDate(startDate);
        SetEndDate(endDate);
        SetSecurityInitializer(security => security.SetFeeModel(new ConstantFeeModel(2.5m))); // $1 per trade
        FinanceData.StartDate = startDate;
        FinanceData.EndDate = endDate;

        var serviceProvider = StaticContainer.ServiceProvider;
        var mainTickersPuller = serviceProvider.GetRequiredService<MainTickersPuller>();
        List<string> tickers = [];
        m_TestingPeriod = Resolution.Minute;
        m_ProblematicTickers = ["HUT", "ENPH"];
        // Define candidate tickers (Big 7, Intel, and other large-cap tech)
        tickers = [
             "OPEN", "SEDG", "HUT", "AAPL"
        ];
        tickers = tickers.Distinct().ToList();
        var rangeAlgorithm = serviceProvider.GetService<RangeAlgorithmsRunner>();
        List<Task> tickersKeyLevelsLoader = [];

        foreach (var ticker in tickers)
        {
            var tickerKeyLevelsLoader = Task.Run(async () =>
            {
                var actualTicker = ticker;
                var range = await rangeAlgorithm!.Run<FinanceCandleStick>(new RangeAlgorithmInput(new PricesPullerParameters(
                    actualTicker,
                    startDateForAlgo,
                    endDateForAlgo,
                    Common.Models.Pullers.Enums.Period.Daily), Algorithm.KeyLevels), CancellationToken.None);
                if (range is not KeyLevelCandleSticks candleSticks) return;
                m_TickerToKeyLevels[actualTicker] = candleSticks.KeyLevels.OrderByDescending(_ => _).ToArray();
            });
            tickersKeyLevelsLoader.Add(tickerKeyLevelsLoader);
        }
        Task.WhenAll(tickersKeyLevelsLoader).Wait();

        var actualTickers = m_TickerToKeyLevels.OrderByDescending(_ => _.Value?.Length ?? 0)
                                               .Select(_ => _.Key)
                                               .ToArray();
        // m_SpyData = rangeAlgorithm!.Run<FinanceCandleStick>(new RangeAlgorithmInput(new PricesPullerParameters(
        //             "ES=F",
        //             startDate,
        //             endDate,
        //             Common.Models.Pullers.Enums.Period.OneMinute), Algorithm.KeyLevels), CancellationToken.None).Result;
        foreach (var ticker in actualTickers)
        {
            if (string.IsNullOrEmpty(ticker) || !m_TickerToKeyLevels.TryGetValue(ticker, out var keyLevels) || keyLevels.Length == 0) continue;
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

        if (!m_TickerToKeyLevels.TryGetValue(ticker, out var keyLevels)) return;
        if (data.Time.Hour < 8) return;
        var symbol = data.Symbol;
        var holdingsq = Securities[symbol].Holdings.Quantity;
        if (holdingsq > 0)
        {
            SellLogic(data);
            return;
        }
        int count = 0;
        foreach (var value in keyLevels)
        {
            var valueDivision = Math.Abs((float)data.CandleStick.Close) / value;
            count++;

            if (valueDivision <= 1.0001 && valueDivision >= 0.9999 && count > 1)
            {
                var previousHistory = History<FinanceData>(data.Symbol, 90, m_TestingPeriod);
                if (previousHistory is not null && previousHistory.Any() && previousHistory.Count() >= 90)
                {

                    var spyResult2 = previousHistory.Select(_ => _.CandleStick).ToList();
                    bool isBullishReversal = spyResult2.Take(spyResult2.Count / 2).All(c => c.Close < c.Open) &&
                    spyResult2.Skip(spyResult2.Count / 2).All(c => c.Close > c.Open);

                    bool isBearishReversal = spyResult2.Take(spyResult2.Count / 2).All(c => c.Close > c.Open) &&
                                                spyResult2.Skip(spyResult2.Count / 2).All(c => c.Close < c.Open);

                    if (!isBearishReversal)
                    {
                        Buy(data.Symbol, data);

                    }

                    else if (data.CandleStick.Pivot == Common.Models.Finance.Enums.Pivot.Low)
                    {
                        Buy(data.Symbol, data);
                        return;
                    }


                }
            }


        }
    }
    private void SellLogic(FinanceData data)
    {
        var holdings = Securities[data.Symbol].Holdings;
        var avgPrice = holdings.AveragePrice;
        var currentPrice = (decimal)data.CandleStick.Close;

        if (holdings.Quantity > 0)
        {


            if (currentPrice >= avgPrice * 1.02m || currentPrice <= avgPrice * 0.983m)
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

    /// <summary>
    /// Executes a buy order for the given symbol.
    /// </summary>
    /// <param name="symbol">The symbol to buy.</param>
    public void Buy(Symbol symbol, FinanceData data)
    {
        Debug($"Trying to buy  {symbol.Value} at price {data.CandleStick.Close}");
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
        SetHoldings(symbol, 0.5);
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
