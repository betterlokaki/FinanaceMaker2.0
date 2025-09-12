using System.Threading.Tasks;
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

/// <summary>
/// Implements a multi-strategy algorithmic trading system that can run multiple trading algorithms simultaneously.
/// Each algorithm is represented by a MultiAlgoModel and can operate independently on different symbols.
/// </summary>
public sealed class MultiAlgorithm : QCAlgorithm
{
    private readonly List<MultiAlgoModel> m_AlgorithmModels = new();
    private readonly Dictionary<string, Action<FinanceData>> m_ExitStrategy = new();
    private readonly Dictionary<string, float> m_TickerToMoney = new();
    private readonly Dictionary<string, int> m_TickerToPosition = new();
    private readonly Dictionary<string, decimal> m_TickerToAvgPrice = new();
    private readonly Dictionary<string, int> m_TickerToLoss = new();
    private readonly Dictionary<string, int> m_TickerToWin = new();
    private readonly Dictionary<string, float[]> m_TickerToKeyLevels = new();
    private Resolution m_TestingPeriod = Resolution.Minute;

    private const decimal DEFAULT_CASH = 9_900m;
    private const decimal TRADE_FEE = 2.5m;

    /// <summary>
    /// Gets the collection of algorithm models currently active in the system.
    /// </summary>
    public IReadOnlyList<MultiAlgoModel> AlgorithmModels => m_AlgorithmModels;

    /// <summary>
    /// Gets the collection of exit strategies mapped to their respective symbols.
    /// </summary>
    public IReadOnlyDictionary<string, Action<FinanceData>> ExitStrategies => m_ExitStrategy;

    /// <summary>
    /// Initializes the algorithm's settings and configurations.
    /// </summary>
    public override void Initialize()
    {
        ConfigureAlgorithmSettings();
        var (startDate, endDate, startDateForAlgo, endDateForAlgo) = GetDateRanges();
        ConfigureDataTimeframe(startDate, endDate);

        var serviceProvider = StaticContainer.ServiceProvider;
        var rangeAlgorithm = serviceProvider.GetRequiredService<RangeAlgorithmsRunner>();
        var mainTickersPuller = serviceProvider.GetRequiredService<MainTickersPuller>();

        // If no algorithms are added yet, add default range algorithm
        if (!m_AlgorithmModels.Any())
        {
            var defaultModel = new MultiAlgoModel(
                DefaultTickers.RangeAlgorithmTickers.Except(DefaultTickers.ProblematicTickers).ToList(),
                OnRangeAlgorithmData
            );
            AddAlgorithm(defaultModel);
        }

        // Get unique tickers from all algorithm models
        var allTickers = m_AlgorithmModels
            .SelectMany(model => model.Tickers)
            .Distinct()
            .ToList();

        LoadKeyLevelsForTickers(allTickers, rangeAlgorithm, startDateForAlgo, endDateForAlgo);
        SetupSecuritiesAndTracking(allTickers);
    }

    private void ConfigureAlgorithmSettings()
    {
        SetCash(DEFAULT_CASH);
        SetSecurityInitializer(security => security.SetFeeModel(new ConstantFeeModel(TRADE_FEE)));
    }

    private static (DateTime startDate, DateTime endDate, DateTime startDateForAlgo, DateTime endDateForAlgo) GetDateRanges()
    {
        var startDate = DateTime.Now.Date.AddDays(-29);
        var startDateForAlgo = new DateTime(2020, 1, 1);
        var endDate = DateTime.Now.AddDays(0);
        var endDateForAlgo = endDate.AddYears(-1).AddMonths(11);

        return (startDate, endDate, startDateForAlgo, endDateForAlgo);
    }

    private void ConfigureDataTimeframe(DateTime startDate, DateTime endDate)
    {
        SetStartDate(startDate);
        SetEndDate(endDate);
        FinanceData.StartDate = startDate;
        FinanceData.EndDate = endDate;
    }

    private void LoadKeyLevelsForTickers(
        List<string> tickers,
        RangeAlgorithmsRunner rangeAlgorithm,
        DateTime startDateForAlgo,
        DateTime endDateForAlgo)
    {
        var tasks = tickers.Select(ticker => Task.Run(async () =>
        {
            var range = await rangeAlgorithm.Run<FinanceCandleStick>(
                new RangeAlgorithmInput(
                    new PricesPullerParameters(
                        ticker,
                        startDateForAlgo,
                        endDateForAlgo,
                        Common.Models.Pullers.Enums.Period.Daily
                    ),
                    Algorithm.KeyLevels
                ),
                CancellationToken.None
            );

            if (range is KeyLevelCandleSticks candleSticks)
            {
                m_TickerToKeyLevels[ticker] = candleSticks.KeyLevels.OrderByDescending(_ => _).ToArray();
            }
        }));

        Task.WhenAll(tasks).Wait();
    }

    private void SetupSecuritiesAndTracking(List<string> tickers)
    {
        foreach (var ticker in tickers)
        {
            if (string.IsNullOrEmpty(ticker) ||
                !m_TickerToKeyLevels.TryGetValue(ticker, out var keyLevels) ||
                keyLevels.Length == 0)
                continue;

            var symbol = AddEquity(ticker, m_TestingPeriod, extendedMarketHours: true);
            AddData<FinanceData>(ticker, m_TestingPeriod);

            InitializeTrackingForTicker(ticker);
        }
    }

    private void InitializeTrackingForTicker(string ticker)
    {
        m_TickerToMoney[ticker] = 0f;
        m_TickerToPosition[ticker] = 0;
        m_TickerToAvgPrice[ticker] = 0m;
    }

    /// <summary>
    /// Adds a new algorithm model to the system.
    /// </summary>
    /// <param name="model">The algorithm model to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when model is null.</exception>
    public void AddAlgorithm(MultiAlgoModel model)
    {
        if (model is null) throw new ArgumentNullException(nameof(model));
        m_AlgorithmModels.Add(model);
    }

    /// <summary>
    /// Adds or updates an exit strategy for a specific symbol.
    /// </summary>
    /// <param name="symbol">The symbol to associate with the exit strategy.</param>
    /// <param name="exitStrategy">The exit strategy action to execute.</param>
    /// <exception cref="ArgumentNullException">Thrown when symbol or exitStrategy is null.</exception>
    public void AddExitStrategy(string symbol, Action<FinanceData> exitStrategy)
    {
        if (string.IsNullOrEmpty(symbol)) throw new ArgumentNullException(nameof(symbol));
        if (exitStrategy is null) throw new ArgumentNullException(nameof(exitStrategy));

        m_ExitStrategy[symbol] = exitStrategy;
    }

    /// <summary>
    /// Removes an exit strategy for a specific symbol.
    /// </summary>
    /// <param name="symbol">The symbol to remove the exit strategy for.</param>
    public void RemoveExitStrategy(string symbol)
    {
        if (string.IsNullOrEmpty(symbol)) return;
        m_ExitStrategy.Remove(symbol);
    }

    /// <summary>
    /// Main data event handler. Processes incoming data and executes corresponding algorithm strategies.
    /// </summary>
    /// <param name="data">The financial data received.</param>
    public void OnData(FinanceData data)
    {
        if (data is null) return;

        var symbol = data.Symbol.Value;

        // Execute exit strategy first if exists
        if (m_ExitStrategy.TryGetValue(symbol, out var exitAction))
        {
            exitAction(data);
            // If position is closed, exit strategy will remove itself
        }

        // Only execute algorithm strategies if we don't have an active exit strategy
        if (!m_ExitStrategy.ContainsKey(symbol))
        {
            // Find and execute matching algorithm strategies
            var matchingAlgorithms = m_AlgorithmModels.Where(model => model.Tickers.Contains(symbol));
            foreach (var algo in matchingAlgorithms)
            {
                algo.OnData(data);
            }
        }
    }

    /// <summary>
    /// Executes a buy order for the given symbol and sets up its exit strategy.
    /// </summary>
    /// <param name="symbol">The symbol to buy.</param>
    public void Buy(Symbol symbol)
    {
        if (symbol is null) throw new ArgumentNullException(nameof(symbol));

        Debug($"Trying to buy {symbol.Value}");
        float position = 1f / m_AlgorithmModels.Count;
        SetHoldings(symbol, position);

        // Add exit strategy for the symbol if not already present
        if (!m_ExitStrategy.ContainsKey(symbol.Value))
        {
            AddExitStrategy(symbol.Value, data =>
            {
                var holdings = Securities[data.Symbol].Holdings;
                if (holdings.Quantity == 0)
                {

                    return;
                }

                var avgPrice = holdings.AveragePrice;
                var currentPrice = (decimal)data.CandleStick.Close;

                if (holdings.Quantity > 0)
                {
                    if (currentPrice >= avgPrice * 1.02m || currentPrice <= avgPrice * 0.98m)
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
                RemoveExitStrategy(data.Symbol.Value);
            });
        }
    }

    /// <summary>
    /// Executes a sell (liquidate) order for the given symbol.
    /// </summary>
    /// <param name="symbol">The symbol to sell.</param>
    public void Sell(Symbol symbol)
    {
        if (symbol is null) throw new ArgumentNullException(nameof(symbol));
        Liquidate(symbol);
        RemoveExitStrategy(symbol.Value);
    }

    /// <summary>
    /// Handles order events to track realized P&L per ticker and manage exit strategies.
    /// </summary>
    /// <param name="orderEvent">The order event.</param>
    public override void OnOrderEvent(OrderEvent orderEvent)
    {
        if (orderEvent is null) throw new ArgumentNullException(nameof(orderEvent));

        if (orderEvent.Status != OrderStatus.Filled && orderEvent.Status != OrderStatus.PartiallyFilled)
            return;

        var symbol = orderEvent.Symbol.Value;
        InitializeTracking(symbol);

        ProcessOrderFill(orderEvent, symbol);

        // Check if position is closed and remove exit strategy if needed
        if (Securities[symbol].Holdings.Quantity == 0)
        {
            RemoveExitStrategy(symbol);
        }
    }

    private void InitializeTracking(string symbol)
    {
        if (!m_TickerToMoney.ContainsKey(symbol))
            m_TickerToMoney[symbol] = 0f;
        if (!m_TickerToPosition.ContainsKey(symbol))
            m_TickerToPosition[symbol] = 0;
        if (!m_TickerToAvgPrice.ContainsKey(symbol))
            m_TickerToAvgPrice[symbol] = 0m;
    }

    private void ProcessOrderFill(OrderEvent orderEvent, string symbol)
    {
        int fillQty = (int)orderEvent.FillQuantity;
        decimal fillPrice = orderEvent.FillPrice;
        decimal orderFee = orderEvent.OrderFee?.Value.Amount ?? 0m;
        int prevPosition = m_TickerToPosition[symbol];
        decimal prevAvgPrice = m_TickerToAvgPrice[symbol];

        if (prevPosition != 0 && Math.Sign(prevPosition) != Math.Sign(fillQty))
        {
            ProcessPositionClose(symbol, fillQty, fillPrice, orderFee, prevPosition, prevAvgPrice);
        }

        UpdatePosition(symbol, fillQty, fillPrice, prevPosition, prevAvgPrice);
    }

    private void ProcessPositionClose(string symbol, int fillQty, decimal fillPrice, decimal orderFee, int prevPosition, decimal prevAvgPrice)
    {
        int closingQty = Math.Abs(Math.Min(Math.Abs(fillQty), Math.Abs(prevPosition)) * Math.Sign(fillQty));
        decimal realized = CalculateRealizedPnL(prevPosition, closingQty, fillPrice, prevAvgPrice, orderFee);

        m_TickerToMoney[symbol] += (float)realized;
        UpdateProfitLossCount(symbol, realized);

        Debug($"[P&L] {symbol} CLOSE {closingQty} @ {fillPrice} (avg {prevAvgPrice}) => Realized: {realized}, Fee: {orderFee}");
    }

    private static decimal CalculateRealizedPnL(int prevPosition, int closingQty, decimal fillPrice, decimal prevAvgPrice, decimal orderFee)
    {
        return prevPosition > 0
            ? closingQty * (fillPrice - prevAvgPrice) - orderFee
            : closingQty * (prevAvgPrice - fillPrice) - orderFee;
    }

    private void UpdateProfitLossCount(string symbol, decimal realized)
    {
        if (realized < 0)
        {
            m_TickerToLoss[symbol] = m_TickerToLoss.TryGetValue(symbol, out int losses) ? losses + 1 : 1;
        }
        else
        {
            m_TickerToWin[symbol] = m_TickerToWin.TryGetValue(symbol, out int wins) ? wins + 1 : 1;
        }
    }

    private void UpdatePosition(string symbol, int fillQty, decimal fillPrice, int prevPosition, decimal prevAvgPrice)
    {
        int totalQty = prevPosition + fillQty;

        if (totalQty == 0)
        {
            m_TickerToAvgPrice[symbol] = 0m;
            m_TickerToPosition[symbol] = 0;
        }
        else if (Math.Sign(fillQty) == Math.Sign(totalQty))
        {
            m_TickerToAvgPrice[symbol] = (prevAvgPrice * prevPosition + fillPrice * fillQty) / totalQty;
            m_TickerToPosition[symbol] = totalQty;
        }
        else
        {
            m_TickerToAvgPrice[symbol] = fillPrice;
            m_TickerToPosition[symbol] = totalQty;
        }
    }

    /// <summary>
    /// Called at the end of the algorithm to display final results.
    /// </summary>
    public override void OnEndOfAlgorithm()
    {
        Debug("--- Per-Ticker Realized P&L ---");
        var sortedResults = m_TickerToMoney
            .OrderByDescending(kvp => kvp.Value)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        foreach (var (ticker, pnl) in sortedResults)
        {
            Debug($"Ticker: {ticker}, Realized P&L: {pnl:C2}");
        }
    }

    /// <summary>
    /// Default data handler for range algorithm strategy.
    /// </summary>
    private void OnRangeAlgorithmData(FinanceData data)
    {
        if (data is null || data.Time.Hour < 8) return;

        var ticker = data.Symbol.Value;
        if (!m_TickerToKeyLevels.TryGetValue(ticker, out var keyLevels)) return;

        ProcessRangeStrategy(data, keyLevels);
    }

    private void ProcessRangeStrategy(FinanceData data, float[] keyLevels)
    {
        int count = 0;
        foreach (var value in keyLevels)
        {
            var valueDivision = Math.Abs((float)data.CandleStick.Close) / value;
            count++;

            if (valueDivision <= 1.0001 && valueDivision >= 0.9999 && count > 1)
            {
                ProcessPotentialEntry(data);
                break;
            }
        }
    }

    private void ProcessPotentialEntry(FinanceData data)
    {
        var symbol = data.Symbol;
        var holdings = Securities[symbol].Holdings;

        if (holdings.Quantity > 0) return;

        var previousHistory = History<FinanceData>(symbol, 90, m_TestingPeriod);
        if (previousHistory?.Count() < 90) return;

        var candlesticks = previousHistory?.Select(_ => _.CandleStick).ToList() ?? [];
        var halfCount = candlesticks.Count / 2;

        bool isBearishReversal = candlesticks.Take(halfCount).All(c => c.Close > c.Open) &&
                                candlesticks.Skip(halfCount).All(c => c.Close < c.Open);

        if (!isBearishReversal || data.CandleStick.Pivot == Common.Models.Finance.Enums.Pivot.Low)
        {
            Buy(symbol);
        }
    }
}
