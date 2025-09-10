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
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;

namespace FinanceMaker.BackTester.QCAlggorithms;

public class MoreAlgorithm : QCAlgorithm
{
    private Dictionary<string, float[]> m_TickerToKeyLevels = new();
    private Dictionary<string, float> m_TickerToMoney = new();
    private Resolution m_TestingPeriod;
    private Dictionary<string, RelativeStrengthIndex> m_RsiIndicators = new();
    // Track open position and average price per ticker for P&L calculation
    private Dictionary<string, int> m_TickerToPosition = new();
    private Dictionary<string, decimal> m_TickerToAvgPrice = new();
    private Dictionary<string, VolumeWeightedAveragePriceIndicator> m_VwapIndicators = new();

    public override void Initialize()
    {
        // Now we can test for last month minutely
        var startDate = DateTime.Now.AddDays(-29);
        var startDateForAlgo = new DateTime(2020, 1, 1);
        var endDate = DateTime.Now;
        var endDateForAlgo = endDate.AddYears(-1).AddMonths(-11);
        SetCash(1_900);
        SetStartDate(startDate);
        SetEndDate(endDate);
        SetSecurityInitializer(security => security.SetFeeModel(new ConstantFeeModel(2.5m))); // $1 per trade
        FinanceData.StartDate = startDate;
        FinanceData.EndDate = endDate;

        var serviceProvider = StaticContainer.ServiceProvider;
        var mainTickersPuller = serviceProvider.GetRequiredService<MainTickersPuller>();
        List<string> tickers = mainTickersPuller.ScanTickers(TechnicalIdeaInput.BestBuyers.TechnicalParams, CancellationToken.None).Result.ToList();
        var random = new Random();
        tickers = [
            //Bitcoin miners
            "HUT",
            // Cars
            "TSLA", 
            // Best buyers
            "NVDA",  "AMD", "BABA", "ENPH", "PLTR",
            // Big 7
            "AAPL", "MSFT", "GOOGL", "NVDA", "TSLA",
            // More large-cap tech
            "NFLX", "CRM", "ORCL", "AVGO", "CSCO",  "AMD"
        ];
        // var tickersNumber = 20;
        // tickers = tickers.OrderBy(_ => random.Next()).Take(tickersNumber).ToList();
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
                    endDateForAlgo, // I removed some years which make the algorithm to be more realistic
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
            if (string.IsNullOrEmpty(ticker) ||
                !m_TickerToKeyLevels.TryGetValue(ticker, out var keyLevels) ||
                 keyLevels.Length == 0) continue;
            var equity = AddEquity(ticker, Resolution.Minute);
            AddData<FinanceData>(equity.Symbol, Resolution.Minute);
            var rsi = RSI(equity.Symbol, 14, MovingAverageType.Simple, Resolution.Minute);
            RegisterIndicator(equity.Symbol, rsi, Resolution.Minute);
            WarmUpIndicator(equity.Symbol, rsi, TimeSpan.FromDays(2));
            m_RsiIndicators[ticker] = rsi;
            var vwap = new VolumeWeightedAveragePriceIndicator($"{ticker}-VWAP", 14);
            RegisterIndicator(equity.Symbol, vwap, Resolution.Minute);
            m_VwapIndicators[ticker] = vwap;
        }
    }

    public void OnData(FinanceData data)
    {
        var ticker = data.Symbol.Value;
        var holdings = Securities[data.Symbol].Holdings;
        var avgPrice = holdings.AveragePrice;
        var currentPrice = (decimal)data.CandleStick.Close;

        if (holdings.Quantity > 0)
        {
            if (currentPrice >= avgPrice * 1.03m || currentPrice <= avgPrice * 0.98m)
            {
                Sell(data.Symbol);

                return;
            }
        }
        m_RsiIndicators[ticker].Update(new IndicatorDataPoint(data.Symbol, data.Time, (decimal)data.CandleStick.Close)); // Example price
        var financeCandleStick = data.CandleStick;
        m_VwapIndicators[ticker].Update(new TradeBar(data.Time, data.Symbol, (decimal)financeCandleStick.Open, (decimal)financeCandleStick.High, (decimal)financeCandleStick.Close, (decimal)financeCandleStick.Low, (decimal)financeCandleStick.Volume)); // Example price
        // 1. Key level test
        if (!m_TickerToKeyLevels.TryGetValue(ticker, out var keyLevels) || keyLevels.Length == 0) return;
        var nearestKeyLevel = keyLevels.OrderBy(k => Math.Abs(k - financeCandleStick.Close)).First();

        var distance = Math.Abs(financeCandleStick.Close - nearestKeyLevel);
        if (distance > financeCandleStick.Close * 0.01f) return; // skip if too far from key level

        // 2. RSI recovery
        if (!m_RsiIndicators.TryGetValue(ticker, out var rsi)) return;
        if (!rsi.IsReady || rsi.Current.Value > 40 || rsi.Current.Value < 20) return;

        // 3. VWAP check
        if (!m_VwapIndicators.TryGetValue(ticker, out var vwap) || !vwap.IsReady) return;
        if (financeCandleStick.Close < (float)vwap.Current.Value) return; // skip if price is below VWAP 

        // 4. Bullish reversal
        var previousClose = History<FinanceData>(data.Symbol, 2, Resolution.Minute).FirstOrDefault()?.CandleStick.Close ?? 0;
        if (financeCandleStick.Close < (float)previousClose) return; // candle is not bullish

        // 5. Entry
        if (!Portfolio[data.Symbol].Invested)
        {
            Buy(data.Symbol); // Buy 10 shares
            Debug($"Bought {ticker} at {financeCandleStick.Close} (VWAP: {vwap}, RSI: {rsi.Current.Value})");
        }
    }


    /// <summary>
    /// Buy this symbol
    /// </summary>
    public void Buy(Symbol symbol)
    {
        //if (_macdDic[symbol] > 0m)
        //{


        SetHoldings(symbol, 0.5);

        //Debug("Purchasing: " + symbol + "   MACD: " + _macdDic[symbol] + "   RSI: " + _rsiDic[symbol]
        //    + "   Price: " + Math.Round(Securities[symbol].Price, 2) + "   Quantity: " + s.Quantity);
        //}
    }

    /// <summary>
    /// Sell this symbol
    /// </summary>
    /// <param name="symbol"></param>
    public void Sell(Symbol symbol)
    {
        //var s = Securities[symbol].Holdings;
        //if (s.Quantity > 0 && _macdDic[symbol] < 0m)
        //{
        Liquidate(symbol);

        //Debug("Selling: " + symbol + " at sell MACD: " + _macdDic[symbol] + "   RSI: " + _rsiDic[symbol]
        //    + "   Price: " + Math.Round(Securities[symbol].Price, 2) + "   Profit from sale: " + s.LastTradeProfit);
        //}
    }
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
