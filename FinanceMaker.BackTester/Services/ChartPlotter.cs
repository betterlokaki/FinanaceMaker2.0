using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FinanceMaker.BackTester.Services.Interfaces;
using FinanceMaker.Common.Models.Finance;
using FinanceMaker.Common.Models.Trading;

namespace FinanceMaker.BackTester.Services
{
    public class ChartPlotter : IChartPlotter
    {
        public async Task<string> CreateTradeChartAsync(TradeVisualizationData visualizationData,
                                                      CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var priceData = visualizationData.PriceData?.ToArray() ?? Array.Empty<FinanceCandleStick>();
                if (priceData.Length == 0)
                    throw new InvalidOperationException("No price data available");

                var times = priceData.Select(p => p.Time.ToString("o")).ToArray();
                var opens = priceData.Select(p => (double)p.Open).ToArray();
                var highs = priceData.Select(p => (double)p.High).ToArray();
                var lows = priceData.Select(p => (double)p.Low).ToArray();
                var closes = priceData.Select(p => (double)p.Close).ToArray();

                var entryX = visualizationData.Trades.Select(t => t.EntryTime.ToString("o")).ToArray();
                var entryY = visualizationData.Trades.Select(t => (double)t.EntryPrice).ToArray();
                var entrySymbols = visualizationData.Trades
                    .Select(t => t.Direction == TradeDirection.Long ? "triangle-down" : "triangle-up").ToArray();
                var entryColors = visualizationData.Trades.Select(t => "green").ToArray();

                var exitX = visualizationData.Trades.Select(t => t.ExitTime.ToString("o")).ToArray();
                var exitY = visualizationData.Trades.Select(t => (double)t.ExitPrice).ToArray();
                var exitSymbols = visualizationData.Trades
                    .Select(t => t.Direction == TradeDirection.Long ? "triangle-down" : "triangle-up").ToArray();
                var exitColors = visualizationData.Trades.Select(t => "red").ToArray();

                var options = new JsonSerializerOptions { WriteIndented = false };
                string ToJson(object obj) => JsonSerializer.Serialize(obj, options);

                var html = $@"
<!doctype html>
<html>
<head>
<meta charset='utf-8'>
<script src='https://cdn.plot.ly/plotly-2.25.2.min.js'></script>
<style>body{{margin:0}}#chart{{width:100%;height:100vh}}</style>
</head>
<body>
<div id='chart'></div>
<script>
const times={ToJson(times)};
const opens={ToJson(opens)};
const highs={ToJson(highs)};
const lows={ToJson(lows)};
const closes={ToJson(closes)};
const entryX={ToJson(entryX)};
const entryY={ToJson(entryY)};
const entrySymbols={ToJson(entrySymbols)};
const entryColors={ToJson(entryColors)};
const exitX={ToJson(exitX)};
const exitY={ToJson(exitY)};
const exitSymbols={ToJson(exitSymbols)};
const exitColors={ToJson(exitColors)};

const candle={{
  x:times,open:opens,high:highs,low:lows,close:closes,
  type:'candlestick',
  increasing:{{line:{{color:'#26a69a'}}}},
  decreasing:{{line:{{color:'#ef5350'}}}},
  name:'Candles'
}};

const entryTrace={{
  x:entryX,y:entryY,mode:'markers',
  marker:{{size:12,color:entryColors,symbol:entrySymbols}},
  name:'Entry'
}};

const exitTrace={{
  x:exitX,y:exitY,mode:'markers',
  marker:{{size:12,color:exitColors,symbol:exitSymbols}},
  name:'Exit'
}};

const layout={{
  title:'{visualizationData.Symbol} - {visualizationData.AlgorithmName}',
  xaxis:{{rangeslider:{{visible:false}},type:'date'}},
  yaxis:{{autorange:true}},
  showlegend:true
}};

Plotly.newPlot('chart',[candle,entryTrace,exitTrace],layout);
</script>
</body>
</html>";

                var filePath = Path.Combine(Path.GetTempPath(),
                    $"{visualizationData.Symbol}_{DateTime.Now:yyyyMMdd_HHmmss}.html");
                File.WriteAllText(filePath, html, Encoding.UTF8);

                try
                {
                    Process.Start(new ProcessStartInfo("/usr/bin/open")
                    {
                        ArgumentList = { "-a", "Google Chrome", filePath },
                        UseShellExecute = false
                    });
                }
                catch
                {
                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                }

                return filePath;
            }, cancellationToken);
        }

        public Task SaveChartAsync(TradeVisualizationData v, string p, CancellationToken c = default)
            => CreateTradeChartAsync(v, c);
    }
}