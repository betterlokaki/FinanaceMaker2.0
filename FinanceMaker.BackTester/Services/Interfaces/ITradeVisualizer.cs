using FinanceMaker.Common.Models.Trading;
using QuantConnect.Orders;

namespace FinanceMaker.BackTester.Services.Interfaces
{
    public interface ITradeVisualizer
    {
        Task<TradeVisualizationData> CreateVisualizationDataAsync(IEnumerable<OrderEvent> orderEvents,
                                                                string symbol,
                                                                string algorithmName,
                                                                DateTime startDate,
                                                                DateTime endDate,
                                                                CancellationToken cancellationToken = default);
    }
}
