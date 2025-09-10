// Licensed to the .NET Foundation under one or more agreements.

namespace FinanceMaker.Common.Models.Pullers.News.NewsResult
{
    public class NewsResult
    {
        public static NewsResult Empty 
            => new(string.Empty);

        public string Url { get; set; }
        public string[] Tickers { get; set; }
        public string Summery { get; set; }

        public NewsResult(string url, string[] tickers, string summery)
        {
            Url = url;
            Tickers = tickers;
            Summery = summery;
        }

        public NewsResult(string url)
        {
            Url = url;
            Tickers = [];
            Summery = string.Empty;
        }

        public bool IsEmpty()
            => string.IsNullOrEmpty(Url);
    }
}
