using FinanceMaker.BackTester.Services.Interfaces;
using FinanceMaker.Common.Models.Finance;
using FinanceMaker.Common.Models.Trading;
using ScottPlot;

namespace FinanceMaker.BackTester.Services
{
    public class ChartPlotter : IChartPlotter
    {
        public async Task<string> CreateTradeChartAsync(TradeVisualizationData visualizationData,
                                                      CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var plot = new Plot();

                // Configure the plot
                plot.Title($"{visualizationData.Symbol} - {visualizationData.AlgorithmName}");
                plot.XLabel("Time");
                plot.YLabel("Price");

                // Convert price data to arrays for plotting
                var priceData = visualizationData.PriceData.ToArray();
                if (priceData.Length == 0)
                {
                    return "No price data available";
                }

                var times = priceData.Select(p => p.Time.ToOADate()).ToArray();
                var closes = priceData.Select(p => (double)p.Close).ToArray();

                // Add price line chart
                plot.Add.Scatter(times, closes, Colors.Blue);

                // Add trade markers
                foreach (var trade in visualizationData.Trades)
                {
                    // Entry marker
                    var entryTime = trade.EntryTime.ToOADate();
                    var entryMarker = plot.Add.Marker(entryTime, (double)trade.EntryPrice);
                    entryMarker.Color = trade.Direction == TradeDirection.Long ? Colors.Green : Colors.Red;
                    entryMarker.Size = 15;
                    entryMarker.LegendText = $"Entry: {trade.EntryPrice:C}";

                    // Exit marker
                    var exitTime = trade.ExitTime.ToOADate();
                    var exitMarker = plot.Add.Marker(exitTime, (double)trade.ExitPrice);
                    exitMarker.Color = trade.Direction == TradeDirection.Long ? Colors.DarkGreen : Colors.DarkRed;
                    exitMarker.Size = 15;
                    exitMarker.LegendText = $"Exit: {trade.ExitPrice:C}";

                    // Add line connecting entry and exit
                    var tradeLine = plot.Add.Line(entryTime, (double)trade.EntryPrice, exitTime, (double)trade.ExitPrice);
                    tradeLine.Color = trade.ProfitLoss >= 0 ? Colors.Green : Colors.Red;
                    tradeLine.LineWidth = 2;
                }

                // Configure axes
                plot.Axes.DateTimeTicksBottom();
                plot.Axes.Margins(bottom: 0.1, left: 0.1);

                // Add legend
                plot.Legend.IsVisible = true;

                // Generate chart as base64 string
                var chartBytes = plot.GetImageBytes(1200, 800);
                return Convert.ToBase64String(chartBytes);
            }, cancellationToken);
        }

        public async Task SaveChartAsync(TradeVisualizationData visualizationData,
                                       string filePath,
                                       CancellationToken cancellationToken = default)
        {
            await Task.Run(() =>
            {
                var plot = new Plot();

                // Configure the plot
                plot.Title($"{visualizationData.Symbol} - {visualizationData.AlgorithmName}");
                plot.XLabel("Time");
                plot.YLabel("Price");

                // Convert price data to arrays for plotting
                var priceData = visualizationData.PriceData.ToArray();
                if (priceData.Length == 0)
                {
                    throw new InvalidOperationException("No price data available");
                }

                var times = priceData.Select(p => p.Time.ToOADate()).ToArray();
                var closes = priceData.Select(p => (double)p.Close).ToArray();

                // Add price line chart
                plot.Add.Candlestick(priceData.Select(_ => new OHLC(_.Open, _.High, _.Low, _.Close, _.Time, TimeSpan.FromMinutes(1))).ToArray());

                // Add trade markers
                foreach (var trade in visualizationData.Trades)
                {
                    // Entry marker
                    var entryTime = trade.EntryTime.ToOADate();
                    var entryMarker = plot.Add.Marker(entryTime, (double)trade.EntryPrice);
                    entryMarker.Color = Colors.Green;
                    entryMarker.Size = 15;
                    entryMarker.LegendText = $"Entry: {trade.EntryPrice:C}";

                    // Exit marker
                    var exitTime = trade.ExitTime.ToOADate();
                    var exitMarker = plot.Add.Marker(exitTime, (double)trade.ExitPrice);
                    exitMarker.Color = Colors.DarkRed;
                    exitMarker.Size = 15;
                    exitMarker.LegendText = $"Exit: {trade.ExitPrice:C}";

                    // Add line connecting entry and exit
                    var tradeLine = plot.Add.Line(entryTime, (double)trade.EntryPrice, exitTime, (double)trade.ExitPrice);
                    tradeLine.Color = trade.ProfitLoss >= 0 ? Colors.Green : Colors.Red;
                    tradeLine.LineWidth = 2;
                }

                // Configure axes
                plot.Axes.DateTimeTicksBottom();
                plot.Axes.Margins(bottom: 0.1, left: 0.1);

                // Add legend
                plot.Legend.IsVisible = true;

                // Save chart to file
                plot.SavePng(filePath, 1200, 800);
            }, cancellationToken);
        }
    }
}