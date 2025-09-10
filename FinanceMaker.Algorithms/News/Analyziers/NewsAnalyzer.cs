using FinanceMaker.Algorithms.News.Analyziers.Interfaces;
using FinanceMaker.Common.Models.Algorithms.Analyzers;
using FinanceMaker.Common.Models.Pullers;

namespace FinanceMaker.Algorithms.News.Analyziers;

public class NewsAnalyzer : INewsAnalyzer
{
    private readonly INewsAnalyzer[] m_Analyzers;

    public NewsAnalyzer(INewsAnalyzer[] analyzers)
    {
        m_Analyzers = analyzers;
    }

    public async Task<IEnumerable<NewsAnalyzed>> AnalyzeNews(
        NewsPullerParameters newsAnalyzerInput, CancellationToken cancellationToken)
    {
        var analyzedTask = m_Analyzers.Select(_ => _.AnalyzeNews(newsAnalyzerInput, cancellationToken));
        var analysed = await Task.WhenAll(analyzedTask);

        return analysed.SelectMany(_ => _);
    }
}
