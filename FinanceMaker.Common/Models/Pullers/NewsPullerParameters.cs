namespace FinanceMaker.Common.Models.Pullers
{
    public class NewsPullerParameters
    {
        public string Ticker { get; set; }
        public DateTime From { get; set; }
        public DateTime To { get; set; }

        public NewsPullerParameters(string ticker, DateTime from, DateTime to)
        {
            Ticker = ticker;
            From = from;
            To = to;
        }

        public static NewsPullerParameters GetTodayParams(string ticker)
        {
            return new(ticker, DateTime.Now, DateTime.Now.Subtract(TimeSpan.FromDays(1)));
        }

    }
}

