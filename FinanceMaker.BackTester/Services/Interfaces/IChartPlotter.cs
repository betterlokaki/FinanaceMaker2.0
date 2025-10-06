using FinanceMaker.Common.Models.Trading;

namespace FinanceMaker.BackTester.Services.Interfaces
{
    public interface IChartPlotter
    {
        Task<string> CreateTradeChartAsync(TradeVisualizationData visualizationData,
                                         CancellationToken cancellationToken = default);

        Task SaveChartAsync(TradeVisualizationData visualizationData,
                          string filePath,
                          CancellationToken cancellationToken = default);
    }
}
