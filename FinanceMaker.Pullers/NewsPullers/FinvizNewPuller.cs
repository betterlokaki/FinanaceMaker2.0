// Licensed to the .NET Foundation under one or more agreements.

using FinanceMaker.Common.Extensions;
using FinanceMaker.Common.Models.Pullers;
using FinanceMaker.Common.Models.Pullers.News.NewsResult;
using FinanceMaker.Pullers.NewsPullers.Interfaces;
using HtmlAgilityPack;

namespace FinanceMaker.Pullers.NewsPullers
{
    public sealed class FinvizNewPuller : INewsPuller
    {
        private readonly IHttpClientFactory m_ClientFactory;
        private readonly string m_FinvizNewUrl;
        private readonly string m_NewXpath;

        public FinvizNewPuller(IHttpClientFactory clientFactory)
        {
            m_ClientFactory = clientFactory;
            m_FinvizNewUrl = "https://finviz.com/news.ashx?v=3";
            m_NewXpath = "//tr[@class=\"styled-row is-hoverable is-bordered is-rounded is-border-top is-hover-borders has-color-text news_table-row\"]";
        }

        public async Task<IEnumerable<NewsResult>> PullNews(
            NewsPullerParameters ticker, CancellationToken cancellationToken)
        {
            var httpClient = m_ClientFactory.CreateClient();
            httpClient.AddBrowserUserAgent();
            var finvizResult = await httpClient.GetAsync(m_FinvizNewUrl);

            if (!finvizResult.IsSuccessStatusCode)
            {
                throw new NotSupportedException($"Something went wrong with finviz {finvizResult.RequestMessage}");
            }

            var finvizHtml = await finvizResult.Content.ReadAsStringAsync();
            var node = new HtmlDocument();
            node.LoadHtml(finvizHtml);

            var nodes = node.DocumentNode.SelectNodes(m_NewXpath);
            var news = nodes.Select(CreateNewsResult)
                            .ToArray();

            return news;
        }

        private NewsResult CreateNewsResult(HtmlNode node)
        {
            var urlA = node.SelectInnerSingleNode("//a[@class=\"nn-tab-link\"]");
            var ticker = node.SelectInnerSingleNode("//span[@class=\"select-none font-semibold\"]").InnerText;
            var url = urlA.Attributes["href"].Value;
            var summery = urlA.InnerText;

            return new(url, [ticker],  summery);
        }
    }
}
