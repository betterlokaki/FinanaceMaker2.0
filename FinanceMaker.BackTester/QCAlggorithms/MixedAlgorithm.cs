using System;
using FinanceMaker.Algorithms;
using FinanceMaker.BackTester.QCHelpers;
using FinanceMaker.Common;
using FinanceMaker.Common.Models.Finance;
using FinanceMaker.Common.Models.Ideas.IdeaInputs;
using FinanceMaker.Pullers.TickerPullers;
using Microsoft.Extensions.DependencyInjection;
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Orders.Fees;

namespace FinanceMaker.BackTester.QCAlggorithms;

public class MixedAlgorithm : QCAlgorithm
{
    private Dictionary<string, float[]> m_TickerToKeyLevels = new();
    private Dictionary<string, RelativeStrengthIndex> m_RsiIndicators = new();
    private Dictionary<string, VolumeWeightedAveragePriceIndicator> m_VwapIndicators = new();

    public override void Initialize()
    {
        var startDate = DateTime.Now.AddDays(-29);
        var startDateForAlgo = new DateTime(2020, 1, 1);
        var endDate = DateTime.Now;
        var endDateForAlgo = endDate.AddYears(-1).AddMonths(-11);
        SetCash(3_000);
        SetStartDate(startDate);
        SetEndDate(endDate);
        SetSecurityInitializer(security => security.SetFeeModel(new ConstantFeeModel(1m))); // $1 per trade
        FinanceData.StartDate = startDate;
        FinanceData.EndDate = endDate;

        var serviceProvider = StaticContainer.ServiceProvider;
        var mainTickersPuller = serviceProvider.GetRequiredService<MainTickersPuller>();
        List<string> tickers = mainTickersPuller.ScanTickers(TechnicalIdeaInput.BestBuyers.TechnicalParams, CancellationToken.None).Result.ToList();
        tickers = ["AAPL", "MSFT", "GOOGL", "AMZN", "TSLA", "META", "NVDA"];
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
        m_RsiIndicators[ticker].Update(new IndicatorDataPoint(data.Symbol, data.Time, (decimal)data.CandleStick.Close));
        var financeCandleStick = data.CandleStick;
        m_VwapIndicators[ticker].Update(new TradeBar(data.Time, data.Symbol, (decimal)financeCandleStick.Open, (decimal)financeCandleStick.High, (decimal)financeCandleStick.Close, (decimal)financeCandleStick.Low, (decimal)financeCandleStick.Volume));

        if (!m_TickerToKeyLevels.TryGetValue(ticker, out var keyLevels) || keyLevels.Length == 0) return;
        var nearestKeyLevel = keyLevels.OrderBy(k => Math.Abs(k - financeCandleStick.Close)).First();

        var distance = Math.Abs(financeCandleStick.Close - nearestKeyLevel);
        if (distance > financeCandleStick.Close * 0.01f) return;

        if (!m_RsiIndicators.TryGetValue(ticker, out var rsi)) return;
        if (!rsi.IsReady || rsi.Current.Value > 40 || rsi.Current.Value < 20) return;

        if (!m_VwapIndicators.TryGetValue(ticker, out var vwap) || !vwap.IsReady) return;
        if (financeCandleStick.Close < (float)vwap.Current.Value) return;

        var previousClose = History<FinanceData>(data.Symbol, 2, Resolution.Minute).FirstOrDefault()?.CandleStick.Close ?? 0;
        if (financeCandleStick.Close < (float)previousClose) return;

        if (!Portfolio[data.Symbol].Invested)
        {
            Buy(data.Symbol);
            Debug($"Bought {ticker} at {financeCandleStick.Close} (VWAP: {vwap}, RSI: {rsi.Current.Value})");
        }
    }

    public void Buy(Symbol symbol)
    {
        SetHoldings(symbol, 0.5);
    }

    public void Sell(Symbol symbol)
    {
        Liquidate(symbol);
    }
}
