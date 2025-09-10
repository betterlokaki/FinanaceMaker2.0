using System.Globalization;
using FinanceMaker.Algorithms;
using FinanceMaker.Common;
using FinanceMaker.Common.Models.Finance;
using FinanceMaker.Common.Models.Finance.Enums;
using FinanceMaker.Common.Models.Pullers.Enums;
using FinanceMaker.Pullers;
using Microsoft.Extensions.DependencyInjection;
using QuantConnect.Configuration;

namespace FinanceMaker.BackTester.QCHelpers;

public static class Helper
{
    public static async Task<string> SaveCandlestickDataToCsv(
     string ticker,
     Period period,
     DateTime startTime,
     DateTime endTime)
    {
        // Ensure the directory exists
        var dataDirectory = Config.Get("data-folder") + "/Custom";
        var filePath = Path.Combine(dataDirectory,
                                    $"{ticker}_{period}_{startTime.Ticks}_{endTime.Ticks}.csv");
        if (File.Exists(filePath)) return filePath;

        Directory.CreateDirectory(dataDirectory);
        // Replace this code with static container
        var serviceProvider = StaticContainer.ServiceProvider;
        var finanaceMaker = serviceProvider.GetRequiredService<RangeAlgorithmsRunner>();
        // Define the file path
        // Create and write to the CSV file
        var candlesticks = await finanaceMaker.Run<EMACandleStick>(new RangeAlgorithmInput(new PricesPullerParameters(ticker, startTime, endTime, period), Algorithm.KeyLevels), CancellationToken.None);
        var candlesAA = candlesticks.Where(_ => _.Pivot != Pivot.Unchanged).ToArray();
        using var writer = new StreamWriter(filePath, false);

        foreach (var candle in candlesticks)
        {

            var line = string.Format(
                "{0:yyyyMMdd HH:mm:ss},{1},{2},{3},{4},{5},{6},{7},{8},{9}",
                candle.Time,
                candle.Open,
                candle.High,
                candle.Low,
                candle.Close,
                candle.Volume,
                candle.EMA,
                (int)candle.EMASignal,
                (int)candle.BreakThrough,
                (int)candle.Pivot
            );
            writer.WriteLine(line);
        }

        return filePath;
    }
}
