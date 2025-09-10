namespace FinanceMaker.Common.Models.Pullers.YahooFinance
{
    public sealed class InterdayModel
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public Chart chart { get; set; }
    }

    public class Chart
    {
        public YahooResponse[] result { get; set; }
        public object error { get; set; }
    }

    public class YahooResponse
    {
        public static YahooResponse Enpty => new YahooResponse();
        public long[] timestamp { get; set; }
        public Indicators indicators { get; set; }

        public bool IsEmpty()
        {
            return timestamp is null || timestamp.Length == 0 ||
                   indicators is null || indicators.quote is null || indicators.quote.Length == 0;
        }

    }

    public class Meta
    {
        public string currency { get; set; }
        public string symbol { get; set; }
        public string exchangeName { get; set; }
        public string fullExchangeName { get; set; }
        public string instrumentType { get; set; }
        public int firstTradeDate { get; set; }
        public int regularMarketTime { get; set; }
        public bool hasPrePostMarketData { get; set; }
        public int gmtoffset { get; set; }
        public string timezone { get; set; }
        public string exchangeTimezoneName { get; set; }
        public float regularMarketPrice { get; set; }
        public float fiftyTwoWeekHigh { get; set; }
        public float fiftyTwoWeekLow { get; set; }
        public float regularMarketDayHigh { get; set; }
        public float regularMarketDayLow { get; set; }
        public int regularMarketVolume { get; set; }
        public float chartPreviousClose { get; set; }
        public float previousClose { get; set; }
        public int scale { get; set; }
        public int priceHint { get; set; }
        public Currenttradingperiod currentTradingPeriod { get; set; }
        public Tradingperiods tradingPeriods { get; set; }
        public string dataGranularity { get; set; }
        public string range { get; set; }
        public string[] validRanges { get; set; }
    }

    public class Currenttradingperiod
    {
        public Pre pre { get; set; }
        public Regular regular { get; set; }
        public Post post { get; set; }
    }

    public class Pre
    {
        public string timezone { get; set; }
        public int start { get; set; }
        public int end { get; set; }
        public int gmtoffset { get; set; }
    }

    public class Regular
    {
        public string timezone { get; set; }
        public int start { get; set; }
        public int end { get; set; }
        public int gmtoffset { get; set; }
    }

    public class Post
    {
        public string timezone { get; set; }
        public int start { get; set; }
        public int end { get; set; }
        public int gmtoffset { get; set; }
    }

    public class Tradingperiods
    {
        public Pre1[][] pre { get; set; }
        public Post1[][] post { get; set; }
        public Regular1[][] regular { get; set; }
    }

    public class Pre1
    {
        public string timezone { get; set; }
        public int start { get; set; }
        public int end { get; set; }
        public int gmtoffset { get; set; }
    }

    public class Post1
    {
        public string timezone { get; set; }
        public int start { get; set; }
        public int end { get; set; }
        public int gmtoffset { get; set; }
    }

    public class Regular1
    {
        public string timezone { get; set; }
        public int start { get; set; }
        public int end { get; set; }
        public int gmtoffset { get; set; }
    }

    public class Indicators
    {
        public Quote[] quote { get; set; }
    }

    public class Quote
    {
        public float?[] high { get; set; }
        public float?[] open { get; set; }
        public float?[] low { get; set; }
        public float?[] close { get; set; }
        public long?[] volume { get; set; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

}

