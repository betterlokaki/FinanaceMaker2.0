using FinanceMaker.Common.Models.Finance;

namespace FinanceMaker.Common.Models.Tickers
{
    public record TickerChart
	{
        public string Ticker { get; set; }
		public FinanceCandleStick[] Prices { get; set; }

        public TickerChart(string ticker, IEnumerable<FinanceCandleStick> price)
        {
            Ticker = ticker;
            Prices = price.ToArray();
        }
    }
}

