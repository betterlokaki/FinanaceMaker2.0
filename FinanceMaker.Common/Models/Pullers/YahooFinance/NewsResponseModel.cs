namespace FinanceMaker.Common.Models.Pullers.YahooFinance
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public class NewsResponseModel
    {
        public Data data { get; set; }
        public string status { get; set; }
    }
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    public class CanonicalUrl
    {
        public string url { get; set; }
        public string site { get; set; }
        public string region { get; set; }
        public string lang { get; set; }
    }

    public class ClickThroughUrl
    {
        public string url { get; set; }
        public string site { get; set; }
        public string region { get; set; }
        public string lang { get; set; }
    }

    public class Content
    {
        public string id { get; set; }
        public string contentType { get; set; }
        public string title { get; set; }
        public bool isHosted { get; set; }
        public DateTime pubDate { get; set; }
        public Thumbnail thumbnail { get; set; }
        public Provider provider { get; set; }
        public object previewUrl { get; set; }
        public CanonicalUrl canonicalUrl { get; set; }
        public ClickThroughUrl clickThroughUrl { get; set; }
        public Finance finance { get; set; }
    }

    public class Data
    {
        public TickerStream tickerStream { get; set; }
    }

    public class Finance
    {
        public List<StockTicker> stockTickers { get; set; }
        public PremiumFinance premiumFinance { get; set; }
    }

    public class Pagination
    {
        public string uuids { get; set; }
    }

    public class PremiumFinance
    {
        public bool isPremiumNews { get; set; }
        public bool isPremiumFreeNews { get; set; }
    }

    public class Provider
    {
        public string displayName { get; set; }
        public string url { get; set; }
    }

    public class Resolution
    {
        public int height { get; set; }
        public int width { get; set; }
        public string url { get; set; }
        public string tag { get; set; }
    }
    public class StockTicker
    {
        public string symbol { get; set; }
    }

    public class Stream
    {
        public string id { get; set; }
        public Content content { get; set; }
    }

    public class Thumbnail
    {
        public List<Resolution> resolutions { get; set; }
    }

    public class TickerStream
    {
        public Pagination pagination { get; set; }
        public bool nextPage { get; set; }
        public List<Stream> stream { get; set; }
    }

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

}
