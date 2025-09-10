namespace FinanceMaker.Common.Models.Tickers
{
    public record TickerNews
	{
        public string Ticker { get; set; }
		public IEnumerable<string> NewsUrl { get; set; }

        public TickerNews(string ticker, IEnumerable<string> newsUrl)
        {
            Ticker = ticker;
            NewsUrl = newsUrl;
        }
	}
}

