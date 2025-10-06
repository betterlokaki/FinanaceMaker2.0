using System.Text.Json.Serialization;

namespace FinanceMaker.Common.Models.Trading
{
    public class TradeData
    {
        public string Symbol { get; set; } = string.Empty;
        public DateTime EntryTime { get; set; }
        public DateTime ExitTime { get; set; }
        public decimal EntryPrice { get; set; }
        public decimal ExitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal ProfitLoss { get; set; }
        public decimal ProfitLossPercent { get; set; }
        public TradeDirection Direction { get; set; }
        public string AlgorithmName { get; set; } = string.Empty;

        public TradeData()
        {
        }

        public TradeData(string symbol, DateTime entryTime, DateTime exitTime,
                        decimal entryPrice, decimal exitPrice, int quantity,
                        TradeDirection direction, string algorithmName)
        {
            Symbol = symbol;
            EntryTime = entryTime;
            ExitTime = exitTime;
            EntryPrice = entryPrice;
            ExitPrice = exitPrice;
            Quantity = quantity;
            Direction = direction;
            AlgorithmName = algorithmName;

            CalculateProfitLoss();
        }

        private void CalculateProfitLoss()
        {
            if (Direction == TradeDirection.Long)
            {
                ProfitLoss = (ExitPrice - EntryPrice) * Quantity;
            }
            else
            {
                ProfitLoss = (EntryPrice - ExitPrice) * Quantity;
            }

            ProfitLossPercent = EntryPrice != 0 ? (ProfitLoss / (EntryPrice * Quantity)) * 100 : 0;
        }
    }

    public enum TradeDirection
    {
        Long,
        Short
    }
}
