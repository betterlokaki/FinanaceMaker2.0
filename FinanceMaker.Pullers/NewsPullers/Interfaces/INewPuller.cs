using FinanceMaker.Common.Models.Pullers;
using FinanceMaker.Common.Models.Pullers.News.NewsResult;

namespace FinanceMaker.Pullers.NewsPullers.Interfaces
{
    public interface INewsPuller
	{
		Task<IEnumerable<NewsResult>> PullNews(NewsPullerParameters ticker, CancellationToken cancellationToken);
	}
}

