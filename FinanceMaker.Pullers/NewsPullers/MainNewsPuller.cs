using FinanceMaker.Common.Models.Pullers;
using FinanceMaker.Common.Models.Pullers.News.NewsResult;
using FinanceMaker.Pullers.NewsPullers.Interfaces;

namespace FinanceMaker.Pullers.NewsPullers
{
    public sealed class MainNewsPuller : INewsPuller
    {
        private readonly INewsPuller[] m_NewsPuller;

        public MainNewsPuller(INewsPuller[] newsPuller)
        {
            m_NewsPuller = newsPuller;
        }

        public async Task<IEnumerable<NewsResult>> PullNews(NewsPullerParameters newsParams, CancellationToken cancellationToken)
        {
            var pullersTasks = m_NewsPuller.Select(puller => puller.PullNews(newsParams, cancellationToken))
                                           .ToArray();

            var newsResult = await Task.WhenAll(pullersTasks);
            var news = newsResult.SelectMany(tickerNews => tickerNews)
                                 .ToArray();

            return news;
        }
    }
}

