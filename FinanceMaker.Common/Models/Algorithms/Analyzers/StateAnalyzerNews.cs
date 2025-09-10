using FinanceMaker.Common.Models.Algorithms.Analyzers.Enums;

namespace FinanceMaker.Common.Models.Algorithms.Analyzers;

public class StateAnalyzerNew : NewsAnalyzed
{
    public NewsStates State { get; set; }

    public StateAnalyzerNew(string news, DateTime date, string ticker, NewsStates state, string description)
    : base(news, date, ticker, description)
    {
        State = state;
    }
    public StateAnalyzerNew(NewsAnalyzed analyzerNewsResult, NewsStates state)
        : base(analyzerNewsResult.News,
               analyzerNewsResult.NewsDate,
               analyzerNewsResult.Ticker,
               analyzerNewsResult.Description)
    {
        State = state;
    }
}
