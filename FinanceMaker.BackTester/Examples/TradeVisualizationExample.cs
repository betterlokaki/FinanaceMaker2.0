using FinanceMaker.BackTester.QCAlggorithms;
using FinanceMaker.BackTester.QCHelpers;
using FinanceMaker.BackTester.Services.Interfaces;

namespace FinanceMaker.BackTester.Examples
{
    public class TradeVisualizationExample
    {
        public static async Task RunExample(CancellationToken cancellationToken = default)
        {
            try
            {
                // Get services from the static container
                var tradeVisualizer = StaticContainer.ServiceProvider.GetService(typeof(ITradeVisualizer)) as ITradeVisualizer;

                if (tradeVisualizer == null)
                {
                    Console.WriteLine("TradeVisualizer service not found. Make sure it's registered in StaticContainer.");
                    return;
                }

                // Run backtest with visualization
                Console.WriteLine("Running backtest with trade visualization...");
                await BackTester.Runner(typeof(FinanceMaker.BackTester.QCAlggorithms.EMAAlgorithm), tradeVisualizer, cancellationToken);

                Console.WriteLine("Trade visualization example completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in TradeVisualizationExample: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}
