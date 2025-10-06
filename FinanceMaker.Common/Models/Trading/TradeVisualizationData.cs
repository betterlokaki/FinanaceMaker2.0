using FinanceMaker.Common.Models.Finance;

namespace FinanceMaker.Common.Models.Trading
{
    public class TradeVisualizationData
    {
        public string Symbol { get; set; } = string.Empty;
        public IEnumerable<FinanceCandleStick> PriceData { get; set; } = Enumerable.Empty<FinanceCandleStick>();
        public IEnumerable<TradeData> Trades { get; set; } = Enumerable.Empty<TradeData>();
        public string AlgorithmName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public TradeVisualizationData()
        {
        }

        public TradeVisualizationData(string symbol, IEnumerable<FinanceCandleStick> priceData,
                                    IEnumerable<TradeData> trades, string algorithmName,
                                    DateTime startDate, DateTime endDate)
        {
            Symbol = symbol;
            PriceData = priceData;
            Trades = trades;
            AlgorithmName = algorithmName;
            StartDate = startDate;
            EndDate = endDate;
        }
    }
}
