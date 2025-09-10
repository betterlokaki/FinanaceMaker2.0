using FinanceMaker.Common.Extensions;
using FinanceMaker.Common.Models.Pullers;
using FinanceMaker.Common.Models.Pullers.News.NewsResult;
using FinanceMaker.Pullers.NewsPullers.Interfaces;
using HtmlAgilityPack;

namespace FinanceMaker.Pullers.NewsPullers
{
    public sealed class GoogleNewsPuller : INewsPuller
    {
        private readonly IHttpClientFactory m_RequestService;
        private readonly string m_NewsUrl;

        public GoogleNewsPuller(IHttpClientFactory requestService)
        {
            m_RequestService = requestService;
            m_NewsUrl = "https://www.google.com/search?q={0}&tbm=nws&hl=en";
        }

        public async Task<IEnumerable<NewsResult>> PullNews(NewsPullerParameters newsParams, CancellationToken cancellationToken)
        {
            var client = m_RequestService.CreateClient();
            client.AddBrowserUserAgent();
            var url = string.Format(m_NewsUrl, newsParams.Ticker);
            var googleResponse = await client.GetAsync(url, cancellationToken);
            var htmlContent = await googleResponse.Content.ReadAsStringAsync(cancellationToken);
            var htmlDocument = new HtmlDocument();

            htmlDocument.LoadHtml(htmlContent);
            var nodes = htmlDocument.DocumentNode.SelectNodes("//a[@class='WlydOe']");
            var hrefs = nodes.Select(node => node.GetAttributeValue("href", string.Empty))
                             .Where(href => !string.IsNullOrEmpty(href))
                             .Select(_ => new NewsResult(_))
                             .ToArray();

            return hrefs;
        }
    }

}

