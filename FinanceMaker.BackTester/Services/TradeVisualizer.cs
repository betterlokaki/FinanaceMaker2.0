using FinanceMaker.BackTester.Services.Interfaces;
using FinanceMaker.Common;
using FinanceMaker.Common.Models.Finance;
using FinanceMaker.Common.Models.Pullers.Enums;
using FinanceMaker.Common.Models.Trading;
using FinanceMaker.Pullers.PricesPullers;
using FinanceMaker.Pullers.PricesPullers.Interfaces;
using QuantConnect;
using QuantConnect.Orders;

namespace FinanceMaker.BackTester.Services
{
    public class TradeVisualizer : ITradeVisualizer
    {
        private readonly IPricesPuller m_PricesPuller;
        private readonly IChartPlotter m_ChartPlotter;

        public TradeVisualizer(IPricesPuller pricesPuller, IChartPlotter chartPlotter)
        {
            m_PricesPuller = pricesPuller;
            m_ChartPlotter = chartPlotter;
        }

        public async Task<TradeVisualizationData> CreateVisualizationDataAsync(IEnumerable<OrderEvent> orderEvents,
                                                                             string symbol,
                                                                             string algorithmName,
                                                                             DateTime startDate,
                                                                             DateTime endDate,
                                                                             CancellationToken cancellationToken = default)
        {
            // Extract trade data from OrderEvents
            var trades = ExtractTradesFromOrderEvents(orderEvents, symbol, algorithmName);

            // Get price data for the same period
            var priceData = await GetPriceDataAsync(symbol, startDate, endDate, cancellationToken);

            return new TradeVisualizationData(symbol, priceData, trades, algorithmName, startDate, endDate);
        }

        private IEnumerable<TradeData> ExtractTradesFromOrderEvents(IEnumerable<OrderEvent> orderEvents, string symbol, string algorithmName)
        {
            var trades = new List<TradeData>();

            try
            {
                // Filter order events for the specific symbol
                var symbolOrderEvents = orderEvents.Where(oe => oe.Symbol.Value == symbol).ToList();

                if (!symbolOrderEvents.Any())
                {
                    Console.WriteLine($"No order events found for symbol: {symbol}");
                    return trades;
                }

                // Group order events by order ID to track complete trades
                var orderGroups = symbolOrderEvents.GroupBy(oe => oe.OrderId).ToList();

                foreach (var orderGroup in orderGroups)
                {
                    var groupOrderEvents = orderGroup.OrderBy(oe => oe.UtcTime).ToList();

                    // Look for fill events (completed trades)
                    var fillEvents = groupOrderEvents.Where(oe => oe.Status == OrderStatus.Filled).ToList();

                    if (fillEvents.Count >= 2)
                    {
                        // We have at least entry and exit fills
                        var entryFill = fillEvents.First();
                        var exitFill = fillEvents.Last();

                        // Determine trade direction based on quantity
                        var direction = entryFill.FillQuantity > 0 ? TradeDirection.Long : TradeDirection.Short;

                        var trade = new TradeData(
                            symbol: symbol,
                            entryTime: entryFill.UtcTime,
                            exitTime: exitFill.UtcTime,
                            entryPrice: entryFill.FillPrice,
                            exitPrice: exitFill.FillPrice,
                            quantity: Math.Abs((int)entryFill.FillQuantity),
                            direction: direction,
                            algorithmName: algorithmName
                        );

                        trades.Add(trade);
                    }
                    else if (fillEvents.Count == 1)
                    {
                        // Only one fill - might be a partial trade or ongoing position
                        var fill = fillEvents.First();
                        var direction = fill.FillQuantity > 0 ? TradeDirection.Long : TradeDirection.Short;

                        // Create a trade with estimated exit (you might want to handle this differently)
                        var trade = new TradeData(
                            symbol: symbol,
                            entryTime: fill.UtcTime,
                            exitTime: fill.UtcTime.AddHours(1), // Placeholder
                            entryPrice: fill.FillPrice,
                            exitPrice: fill.FillPrice * 1.01m, // Placeholder
                            quantity: Math.Abs((int)fill.FillQuantity),
                            direction: direction,
                            algorithmName: algorithmName
                        );

                        trades.Add(trade);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting trades from order events: {ex.Message}");
            }

            return trades;
        }

        private async Task<IEnumerable<FinanceCandleStick>> GetPriceDataAsync(string symbol,
                                                                             DateTime startDate,
                                                                             DateTime endDate,
                                                                             CancellationToken cancellationToken)
        {
            try
            {
                var parameters = new PricesPullerParameters(symbol, startDate, endDate, FinanceMaker.Common.Models.Pullers.Enums.Period.OneMinute);
                return await m_PricesPuller.GetTickerPrices(parameters, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting price data for {symbol}: {ex.Message}");
                return Enumerable.Empty<FinanceCandleStick>();
            }
        }
    }
}
