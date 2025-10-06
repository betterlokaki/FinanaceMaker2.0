# Trade Visualization for FinanceMaker BackTester

This document describes the new trade visualization functionality that allows you to plot and display trade entries and exits over intraday charts of each stock.

## Overview

The trade visualization system extracts trade data from QuantConnect's backtest results and combines it with historical price data to create visual charts showing:
- Intraday price movements
- Trade entry points (marked in green for long positions, red for short positions)
- Trade exit points (marked in darker colors)
- Lines connecting entry and exit points
- Profit/Loss information for each trade

## Components

### 1. TradeData Model (`FinanceMaker.Common/Models/Trading/TradeData.cs`)
Represents a single trade with:
- Symbol, entry/exit times and prices
- Quantity and direction (Long/Short)
- Calculated profit/loss and percentage
- Algorithm name

### 2. TradeVisualizationData Model (`FinanceMaker.Common/Models/Trading/TradeVisualizationData.cs`)
Combines trade data with price data for visualization:
- Symbol and algorithm name
- Historical price data (candlesticks)
- List of trades
- Date range

### 3. ChartPlotter Service (`FinanceMaker.BackTester/Services/ChartPlotter.cs`)
Uses ScottPlot to create charts:
- Generates PNG files with trade visualization
- Supports both file saving and base64 string output
- Uses line charts for price data and markers for trades

### 4. TradeVisualizer Service (`FinanceMaker.BackTester/Services/TradeVisualizer.cs`)
Main service that:
- Extracts trade data from QuantConnect result handlers using reflection
- Fetches historical price data using MainPricesPuller
- Combines data into TradeVisualizationData objects

### 5. Updated BackTester (`FinanceMaker.BackTester/BackTester.cs`)
Enhanced to support trade visualization:
- New `Runner` method accepts optional `ITradeVisualizer` parameter
- Automatically generates charts for all traded symbols
- Saves charts to `Charts/` directory with descriptive filenames

## Usage

### Basic Usage

```csharp
// Get the trade visualizer from the service container
var tradeVisualizer = StaticContainer.ServiceProvider.GetService(typeof(ITradeVisualizer)) as ITradeVisualizer;

// Run backtest with visualization
await BackTester.Runner(typeof(EMAAlgorithm), tradeVisualizer, cancellationToken);
```

### Example Program

The `TradeVisualizationExample` class demonstrates how to use the functionality:

```csharp
public static async Task RunExample(CancellationToken cancellationToken = default)
{
    var tradeVisualizer = StaticContainer.ServiceProvider.GetService(typeof(ITradeVisualizer)) as ITradeVisualizer;
    
    if (tradeVisualizer == null)
    {
        Console.WriteLine("TradeVisualizer service not found.");
        return;
    }

    await BackTester.Runner(typeof(EMAAlgorithm), tradeVisualizer, cancellationToken);
}
```

## Output

When you run a backtest with trade visualization enabled, the system will:

1. **Extract Trade Data**: Uses reflection to access QuantConnect's result handler and extract order information
2. **Fetch Price Data**: Gets historical intraday price data for each traded symbol
3. **Generate Charts**: Creates PNG files in the `Charts/` directory with names like:
   - `AAPL_EMAAlgorithm_20241220_143022.png`
4. **Display Summary**: Prints trade information to the console:
   ```
   Trade chart saved: Charts/AAPL_EMAAlgorithm_20241220_143022.png
   Trades found: 3
     Long 100 shares at $150.00 -> $155.00 P&L: $500.00 (3.33%)
     Short 50 shares at $160.00 -> $158.00 P&L: $100.00 (1.25%)
   ```

## Chart Features

- **Price Line**: Blue line showing intraday price movements
- **Entry Markers**: Colored markers showing trade entry points
  - Green for long positions
  - Red for short positions
- **Exit Markers**: Darker colored markers showing trade exit points
- **Trade Lines**: Lines connecting entry and exit points
  - Green for profitable trades
  - Red for losing trades
- **Legend**: Shows entry/exit information
- **Time Axis**: Properly formatted date/time axis

## macOS Compatibility

The solution is designed to work on macOS:
- Uses ScottPlot 5.0.55 which supports cross-platform development
- Generates PNG files that can be viewed in any image viewer
- No GUI dependencies - all output is file-based
- Compatible with .NET 9.0

## Dependencies

- **ScottPlot 5.0.55**: For chart generation
- **QuantConnect.Lean**: For backtest results access
- **FinanceMaker.Pullers**: For historical price data
- **FinanceMaker.Common**: For shared models and interfaces

## Service Registration

The services are automatically registered in `StaticContainer.cs`:

```csharp
services.AddSingleton<FinanceMaker.BackTester.Services.Interfaces.IChartPlotter, FinanceMaker.BackTester.Services.ChartPlotter>();
services.AddSingleton<FinanceMaker.BackTester.Services.Interfaces.ITradeVisualizer, FinanceMaker.BackTester.Services.TradeVisualizer>();
```

## Error Handling

The system includes comprehensive error handling:
- Graceful handling of missing or invalid trade data
- Fallback behavior when price data is unavailable
- Detailed error logging to console
- Non-blocking errors that don't stop the backtest process

## Future Enhancements

Potential improvements:
- Support for candlestick charts instead of line charts
- Interactive web-based charts
- Export to different formats (SVG, PDF)
- Real-time chart updates during live trading
- Custom chart styling and themes
- Trade performance analytics overlay

## Troubleshooting

### Common Issues

1. **No charts generated**: Check that the algorithm actually executed trades
2. **Missing price data**: Ensure the price puller is properly configured
3. **Compilation errors**: Verify ScottPlot package is correctly installed
4. **Empty charts**: Check that the date range includes the backtest period

### Debug Information

The system provides detailed console output:
- Trade extraction progress
- Price data retrieval status
- Chart generation results
- Error messages with context
