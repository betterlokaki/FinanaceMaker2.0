// Licensed to the .NET Foundation under one or more agreements.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FinanceMaker.Algorithms;
using FinanceMaker.BackTester.QCHelpers;
using FinanceMaker.Common;
using FinanceMaker.Common.Models.Finance;
using FinanceMaker.Common.Models.Finance.Enums;
using FinanceMaker.Common.Models.Ideas.IdeaInputs;
using FinanceMaker.Pullers.TickerPullers;
using Microsoft.Extensions.DependencyInjection;
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Indicators;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;

namespace FinanceMaker.BackTester.QCAlggorithms;

/// <summary>
/// Range trading algorithm using key levels and RSI for entry/exit, with per-ticker realized P&L tracking.
/// </summary>
public class RangeAlgoritm : QCAlgorithm
{
    private Dictionary<string, float[]> m_TickerToKeyLevels = new();
    private Dictionary<string, float> m_TickerToMoney = new();
    private Resolution m_TestingPeriod;
    private Dictionary<string, RelativeStrengthIndex> m_RsiIndicators = new();
    // Track open position and average price per ticker for P&L calculation
    private Dictionary<string, int> m_TickerToPosition = new();
    private Dictionary<string, decimal> m_TickerToAvgPrice = new();

    /// <summary>
    /// Initializes the algorithm, loads tickers and key levels, and sets up securities.
    /// </summary>
    public override void Initialize()
    {
        var startDate = DateTime.Now.AddDays(-29);
        var startDateForAlgo = new DateTime(2020, 1, 1);
        var endDate = DateTime.Now.AddDays(0);
        var endDateForAlgo = endDate.AddYears(-1).AddMonths(11);
        SetCash(9_900); // Starting cash for the algorithm
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
            //Bitcoin miners
            // "HUT", 
            // Cars
            // "TSLA", 
            // Best buyers
            "NVDA",  "AMD", "BABA", "ENPH", "PLTR",
            // Big 7
            "AAPL", "MSFT", "GOOGL", "NVDA",    
            // More large-cap tech
            "CRM", "ORCL", "AVGO", "CSCO",  "AMD"
        ];
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
                m_TickerToKeyLevels[actualTicker] = candleSticks.KeyLevels;
            });
            tickersKeyLevelsLoader.Add(tickerKeyLevelsLoader);
        }
        Task.WhenAll(tickersKeyLevelsLoader).Wait();

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

        foreach (var value in keyLevels)
        {
            var valueDivision = Math.Abs((float)data.CandleStick.Close) / value;
            if (valueDivision <= 1.001 && valueDivision >= 0.995)
            {
                var symbol = data.Symbol.Value;
                var holdingsq = Securities[symbol].Holdings.Quantity;
                if (holdingsq == 0)
                {
                    var previousHistory = History<FinanceData>(data.Symbol, 2, m_TestingPeriod);
                    if (previousHistory is not null && previousHistory.Any())
                    {
                        bool hasTentativePivot = true;
                        var previousList = previousHistory.ToList();
                        for (int i = 0; i < previousList.Count - 1; i++)
                        {
                            if (previousList[i].CandleStick.Close > previousList[i + 1].CandleStick.Close)
                                hasTentativePivot &= true;
                            else
                            {
                                hasTentativePivot = false;
                                break;
                            }
                        }
                        if (hasTentativePivot && previousList.Count > 0)
                        {
                            hasTentativePivot &= previousList.Last().CandleStick.Close > data.CandleStick.Open;
                        }
                        if (hasTentativePivot && (valueDivision <= 1.005 && valueDivision >= 0.995))
                        {

                            Buy(data.Symbol);
                        }
                    }
                    return;
                }
            }
            var holdings = Securities[data.Symbol].Holdings;
            var avgPrice = holdings.AveragePrice;
            var currentPrice = (decimal)data.CandleStick.Close;
            if (holdings.Quantity > 0)
            {
                if (currentPrice >= avgPrice * 1.02m || currentPrice <= avgPrice * 0.985m)
                {
                    Sell(data.Symbol);
                }
            }
            if (holdings.Quantity < 0)
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
    public void Buy(Symbol symbol)
    {
        Debug("Trying to buy " + symbol.Value);
        SetHoldings(symbol, 0.3);
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
