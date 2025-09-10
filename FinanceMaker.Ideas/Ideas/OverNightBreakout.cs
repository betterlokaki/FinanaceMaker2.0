using FinanceMaker.Algorithms;
using FinanceMaker.Algorithms.News.Analyziers.Interfaces;
using FinanceMaker.Common.Models.Ideas.IdeaInputs;
using FinanceMaker.Common.Models.Ideas.IdeaOutputs;
using FinanceMaker.Pullers.TickerPullers;

namespace FinanceMaker.Ideas.Ideas;

public class OverNightBreakout : KeyLevelsEntryExitOutputIdea<TechnicalIdeaInput, EntryExitOutputIdea>
{
    private readonly INewsAnalyzer m_Analyzer;
    public OverNightBreakout(MainTickersPuller puller, RangeAlgorithmsRunner algoRunner, INewsAnalyzer analyzer) : base(puller, algoRunner)
    {
        m_Analyzer = analyzer;
    }

    protected override async Task<IEnumerable<EntryExitOutputIdea>> CreateIdea(TechnicalIdeaInput input, CancellationToken cancellationToken)
    {
        input.TechnicalParams.MaxPresentageOfChange = 100;
        input.TechnicalParams.MinPresentageOfChange = 4;


        var ideas = await base.CreateIdea(input, cancellationToken);
        var from = DateTime.Now.Subtract(TimeSpan.FromDays(1));
        var to = DateTime.Now;

        // foreach (var idea in ideas)
        // {
        //     var data = new NewsAnalyzerInput(idea.Ticker, from, to, []);
        //     var analysed = await m_Analyzer.AnalyzeNews(data, cancellationToken);
        //     idea.Analyzed = analysed.ToArray();

        // }

        return ideas;
    }
}
