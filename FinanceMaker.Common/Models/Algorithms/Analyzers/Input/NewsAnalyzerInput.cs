using FinanceMaker.Common.Models.Pullers;
using FinanceMaker.Common.Models.Pullers.News.NewsResult;

namespace FinanceMaker.Common.Models.Algorithms.Analyzers.Input;

public class NewsAnalyzerInput : NewsPullerParameters
{
    public IEnumerable<NewsResult> NewsResult { get; set; }

    public NewsAnalyzerInput(string ticker, DateTime from, DateTime to, IEnumerable<NewsResult> newResult)
    : base(ticker, from, to)
    {
        NewsResult = newResult;
    }

    public NewsAnalyzerInput(NewsPullerParameters puller, IEnumerable<NewsResult> urls)
        : this(puller.Ticker, puller.From, puller.To, urls)
    { }
}
