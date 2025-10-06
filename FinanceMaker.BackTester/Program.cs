using FinanceMaker.BackTester.Examples;

namespace FinanceMaker.BackTester
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("FinanceMaker BackTester with Trade Visualization");
            Console.WriteLine("================================================");

            try
            {
                // Run the trade visualization example
                await TradeVisualizationExample.RunExample();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
