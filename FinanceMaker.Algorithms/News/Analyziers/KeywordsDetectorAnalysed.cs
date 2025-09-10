// Licensed to the .NET Foundation under one or more agreements.

using FinanceMaker.Algorithms.News.Analyziers.Abstracts;
using FinanceMaker.Common.Models.Algorithms.Analyzers;
using FinanceMaker.Common.Models.Algorithms.Analyzers.Enums;
using FinanceMaker.Common.Models.Algorithms.Analyzers.Input;
using FinanceMaker.Pullers.NewsPullers.Interfaces;
using HtmlAgilityPack;

namespace FinanceMaker.Algorithms.News.Analyziers
{
    public sealed class KeywordsDetectorAnalysed : NewsAnalyzerBase<NewsAnalyzerInput, StateAnalyzerNew>
    {
        private readonly string[] m_GoodWords;
        private readonly string[] m_BadWords;
        private readonly HtmlWeb m_NewsLoader;

        public KeywordsDetectorAnalysed(INewsPuller puller) : base(puller)
        {
            m_GoodWords =
            [
               "Strong earnings",
               "Revenue growth",
               "Upward revision",
               "Beats expectations",
               "Raising guidance",
               "Buyback announcement",
               "Dividend increase",
               "Acquisition/merger",
               "Partnership deal",
               "Expansion plans",
               "New product launch",
               "Record profits",
               "Market share gains",
               "Positive outlook",
               "Outperformance",
               "Analyst upgrade",
                "Bullish trend",
               "Price target raised",
               "CEO confidence",
               "Insider buying",
               "New contracts signed",
               "Technological advancements",
               "Cost-cutting measures",
            ];
            m_BadWords = [
                "Misses expectations",
                "Revenue decline",
                "Lowered guidance",
                "Earnings miss",
                "Losses widen",
                "Profit warning",
                "Dividend cut",
                "Stock downgrade",
                "Sell-off",
                "Underperformance",
                "Bearish trend",
                "Price target lowered",
                "Negative outlook",
                "Disappointing results",
                "Weak demand",
                "Economic slowdown",
                "Recession fears",
                "Sector underperformance",
                "Rising inflation",
                "Interest rate hikes",
                "Regulatory challenges",
                "Weak consumer spending",
                "Supply chain disruptions",
                "CEO resignation",
                "Insider selling",
                "Accounting issues",
                "Product recall",
                "Lawsuit/Legal trouble",
                "Bankruptcy risk",
                "Missed targets",
                "Cost overruns",
                "Failed acquisition/merger",
                "Market volatility",
                "Debt concerns",
                "Share dilution",
                "Weak outlook",
                "Cutbacks",
                "Layoffs",
                "Negative analyst sentiment",
                ];
            m_NewsLoader = new HtmlWeb();
        }

        protected override async Task<IEnumerable<StateAnalyzerNew>> AnalyzeNews(NewsAnalyzerInput input, CancellationToken cancellationToken)
        {
            // For now I'm only gonna pull this using the html agility pack
            // but in the future I will use some intersting web scrapping to load more and more of the articles because many of them are lazy loaded
            // To do that we will need to learn how get the request that a web site does and to do it 
            // Or use selenium 🤮🤮🤮🤮  

            var urls = input.NewsResult.Select(_ => _.Url).ToArray();
            var analysed = new List<StateAnalyzerNew>();

            foreach (var url in urls)
            {
                var article = await m_NewsLoader.LoadFromWebAsync(url, cancellationToken);
                var html = article.DocumentNode.OuterHtml;
                // in the pullers we probably try to get their dates also
                var isGood = m_GoodWords.Count(html.Contains);
                var isBad = m_BadWords.Count(html.Contains);
                var newsStates = NewsStates.None;

                if (isGood > isBad)
                {
                    newsStates = NewsStates.Good;
                }
                else if (isGood < isBad)
                {
                    newsStates = NewsStates.Bad;
                }
                else
                {
                    // if the both zero or equal 
                    newsStates = NewsStates.None;
                }

                var analysedNews = new StateAnalyzerNew(url,
                                                        input.From,
                                                        input.Ticker,
                                                        newsStates,
                                                        $"News are {newsStates}");

                analysed.Add(analysedNews);
            }

            return analysed;
        }
    }
}
