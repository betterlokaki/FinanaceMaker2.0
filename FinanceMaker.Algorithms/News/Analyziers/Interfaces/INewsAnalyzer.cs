using FinanceMaker.Common.Models.Algorithms.Analyzers;
using FinanceMaker.Common.Models.Pullers;

namespace FinanceMaker.Algorithms.News.Analyziers.Interfaces;

public interface INewsAnalyzer
{
    /// <summary>
    /// Analyze the news on the relevant params, this function has more control over the out because you have more input to give 
    /// </summary>
    /// <param name="newsAnalyzerInput">News search and analyze input</param>
    /// <param name="cancellationToken">token</param>
    /// <returns>
    /// The Aanalyzed news
    ///  </returns>
    Task<IEnumerable<NewsAnalyzed>> AnalyzeNews(NewsPullerParameters newsAnalyzerInput, CancellationToken cancellationToken);
}
