using System.Diagnostics;
using FinanceMaker.BackTester.QCHelpers;
using FinanceMaker.BackTester.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using QuantConnect;
using QuantConnect.Configuration;
using QuantConnect.Lean.Engine;
using QuantConnect.Orders;
using QuantConnect.Util;

namespace FinanceMaker.BackTester;

public class BackTester
{
    /// <summary>
    /// This project is a backtester for the FinanceMaker project.
    /// Foreach algorithm we want to backtest it using the quantconnect SDK, 
    /// 
    /// Problem -
    ///     QuantConnect does not support the creation of algorithm instances using
    ///     a factory pattern
    ///
    /// Solution -
    ///     1. We may add them as submodule and change 
    ///        it (I really don't want to do this)
    ///     
    ///    2. For each algorithm we want to backtest, 
    ///        we initiaze the container for it like we did in the csv function
    ///       
    /// Now I'm not exactly sure what will be the future for this project
    /// So I need to decide if we want to continue with the dynamic Traders 
    /// By choosing the best algorithm for the current stock 
    /// (by running on it multiple algorithms and choosing the best one) 
    /// And then starts trading with this, 
    ///  
    /// Or 
    /// 
    /// Run do it by hand and not automate it,
    /// 
    /// I think the best aproch is to create the echo system which allow me
    /// To get the intersting tickers for today run on each of them all the algorithms
    /// and the backtesting, and then choose the best algorithm, 
    /// get the buy and sell recommendations and then trade on them by hand
    /// 
    /// input - Nothing
    /// Output - List of stocks and the ideas to how to trade them
    /// </summary>
    /// 

    public static async Task Runner(Type algorithm, ITradeVisualizer? tradeVisualizer = null, CancellationToken cancellationToken = default)
    {
        Config.Set("algorithm-type-name", algorithm.Name);
        Config.Set("data-folder", "../../../../FinanceMaker.BackTester/Data");
        Config.Set("algorithm-language", "CSharp");
        Config.Set("algorithm-location", "FinanceMaker.BackTester.dll");

        //Name thread for the profiler:
        Thread.CurrentThread.Name = "Algorithm Analysis Thread";

        Initializer.Start();
        var bruh = StaticContainer.ServiceProvider;
        var leanEngineSystemHandlers = Initializer.GetSystemHandlers();

        //-> Pull job from QuantConnect job queue, or, pull local build:
        var job = leanEngineSystemHandlers.JobQueue.NextJob(out var assemblyPath);

        var leanEngineAlgorithmHandlers = Initializer.GetAlgorithmHandlers();

        // Create the algorithm manager and start our engine
        var algorithmManager = new AlgorithmManager(QuantConnect.Globals.LiveMode, job);

        leanEngineSystemHandlers.LeanManager.Initialize(leanEngineSystemHandlers, leanEngineAlgorithmHandlers, job, algorithmManager);

        OS.Initialize();

        var engine = new Engine(leanEngineSystemHandlers, leanEngineAlgorithmHandlers, QuantConnect.Globals.LiveMode);
        engine.Run(job, algorithmManager, assemblyPath, WorkerThread.Instance);

        var dataFolder = Config.Get("data-folder");
        var customDataDirectory = Path.Combine(dataFolder, "Custom");
        var resultHandler = engine.AlgorithmHandlers.Results;
        var p = engine.AlgorithmHandlers.Transactions.OrderEvents;
        var data = FinanceData.CounterDataSource;
        var tradeVisualizer1 = StaticContainer.ServiceProvider.GetRequiredService<ITradeVisualizer>();

        // Generate trade visualization if tradeVisualizer is provided
        if (tradeVisualizer1 != null && resultHandler != null)
        {
            try
            {
                await GenerateTradeVisualization(p, algorithm.Name, tradeVisualizer1, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating trade visualization: {ex.Message}");
            }
        }

        if (Directory.Exists(customDataDirectory))
        {
            Directory.Delete(customDataDirectory, true);
        }
    }

    private static async Task GenerateTradeVisualization(IEnumerable<OrderEvent> orderEvents, string algorithmName, ITradeVisualizer tradeVisualizer, CancellationToken cancellationToken)
    {
        try
        {
            // Get the symbols that were traded from OrderEvents
            var symbols = orderEvents
                .Select(oe => oe.Symbol.Value)
                .Distinct()
                .ToList();

            Console.WriteLine($"Found {symbols.Count} symbols with trades: {string.Join(", ", symbols)}");

            foreach (var symbol in symbols)
            {
                // Create visualization data
                var visualizationData = await tradeVisualizer.CreateVisualizationDataAsync(
                    orderEvents,
                    symbol,
                    algorithmName,
                    FinanceData.StartDate,
                    FinanceData.EndDate,
                    cancellationToken);

                if (visualizationData.Trades.Any())
                {
                    // Save chart to file
                    var fileName = $"{symbol}_{algorithmName}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                    var filePath = Path.Combine("Charts", fileName);

                    // Ensure Charts directory exists
                    Directory.CreateDirectory("Charts");

                    var chartPlotter = new FinanceMaker.BackTester.Services.ChartPlotter();
                    await chartPlotter.SaveChartAsync(visualizationData, filePath, cancellationToken);

                    Console.WriteLine($"Trade chart saved: {filePath}");
                    Console.WriteLine($"Trades found: {visualizationData.Trades.Count()}");

                    // Print trade summary
                    foreach (var trade in visualizationData.Trades)
                    {
                        Console.WriteLine($"  {trade.Direction} {trade.Quantity} shares at {trade.EntryPrice:C} -> {trade.ExitPrice:C} " +
                                        $"P&L: {trade.ProfitLoss:C} ({trade.ProfitLossPercent:F2}%)");
                    }
                }
                else
                {
                    Console.WriteLine($"No trades found for symbol: {symbol}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GenerateTradeVisualization: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    // All that left is to connect the algorithm to the worker, so do it in the bus,
    // We want to use the keylevel algorithm to get the key levels for the stock
    // Then buy the stock if its pivot low, that's it
    // simple as that, and the result might be amzing, so far it did like 873% on the back testing
    // therefore we must check it on real life trading.
    // We don't need to connect the backtester to the worker, we just need to implement this logic, in the worker
    // I don't know ennglish that well its just using the Copilot
}