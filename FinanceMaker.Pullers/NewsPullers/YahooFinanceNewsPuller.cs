using FinanceMaker.Common.Extensions;
using FinanceMaker.Common.Models.Pullers;
using FinanceMaker.Common.Models.Pullers.News.NewsResult;
using FinanceMaker.Common.Models.Pullers.YahooFinance;
using FinanceMaker.Pullers.NewsPullers.Interfaces;

namespace FinanceMaker.Pullers.NewsPullers
{
    public class YahooFinanceNewsPuller : INewsPuller
    {
        private readonly IHttpClientFactory m_RequestService;
        private readonly string m_MainTickerPage;

        public YahooFinanceNewsPuller(IHttpClientFactory httpClientFactory)
        {
            m_RequestService = httpClientFactory;
            m_MainTickerPage = "https://finance.yahoo.com/xhr/ncp?location=US&queryRef=qsp&serviceKey=ncp_fin&symbols={0}&lang=en-US&region=US";
        }

        public async Task<IEnumerable<NewsResult>> PullNews(NewsPullerParameters newsParams, CancellationToken cancellationToken)
        {
            var client = m_RequestService.CreateClient()
                                         .AddBrowserUserAgent();
            var mainPage = string.Format(m_MainTickerPage, newsParams.Ticker);

            var requestBody = NewsRequestModel.CreateCloneToYahoo();
            var response = await client.PostAsJsonAsync(mainPage, requestBody, cancellationToken);

            if (!response.IsSuccessStatusCode || !cancellationToken.IsCancellationRequested)
            {
                return [];
            }

            var yahooResult = await response.Content.ReadAsAsync<NewsResponseModel>(cancellationToken);

            if (yahooResult is null || yahooResult.status != "OK")
            {
                return [];
            }

            var newsStream = yahooResult?.data?.tickerStream?.stream;

            if (newsStream is null)
            {
                return [];
            }

            var news = newsStream.Select(_ => new NewsResult(_.content.canonicalUrl.url))
                                 .ToArray();

            return news;
        }
    }
}

