using System;

namespace FinanceMaker.BackTester.QCAlggorithms;

using QuantConnect;
using QuantConnect.Configuration;
using QuantConnect.Lean.Engine;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Lean.Engine.RealTime;
using QuantConnect.Lean.Engine.Results;
using QuantConnect.Lean.Engine.Setup;
using QuantConnect.Lean.Engine.TransactionHandlers;
using QuantConnect.Util;

public static class LeanLauncher
{
    public static void StartLiveAlpaca()
    {
        Config.Set("environment", "live");
        Config.Set("live-mode-brokerage", "Alpaca");
        Config.Set("alpaca-key-id", "PKH61BCHIWNB11A588E2");
        Config.Set("alpaca-secret-key", "kAHcVCqGcOLlwo0ZEbBI60WVtzoXnTvq8YPT9roz");
        Config.Set("alpaca-trading-mode", "live");
        Config.Set("live-data-provider", "Alpaca");
        Config.Set("algorithm-type-name", "FinanceMaker.BackTester.QCAlggorithms.RangeAlgoritm");
        Config.Set("algorithm-location", "FinanceMaker.BackTester.dll");
        Config.Set("data-folder", "../../../../FinanceMaker.BackTester/Data");
        //Name thread for the profiler:
        Thread.CurrentThread.Name = "Algorithm Analysis Thread";

        Initializer.Start();
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

    }

}

public class LiveProvider : ITimeProvider
{
    public DateTime GetUtcNow()
    {
        return DateTime.UtcNow;
    }
}
