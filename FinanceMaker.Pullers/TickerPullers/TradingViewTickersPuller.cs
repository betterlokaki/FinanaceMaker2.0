using System.Net.Http.Json;
using System.Text;
using FinanceMaker.Common.Extensions;
using FinanceMaker.Common.Models.Pullers;
using FinanceMaker.Common.Models.Pullers.TradingView;
using FinanceMaker.Pullers.TickerPullers.Interfaces;

namespace FinanceMaker.Pullers.TickerPullers;

public class TradingViewTickersPuller : IParamtizedTickersPuller
{
    private readonly IHttpClientFactory m_RequestService;
    private readonly string m_TradingViewUrl;
    private readonly string m_RequestBody;
    private readonly HttpRequestMessage m_RequestContent;

    public TradingViewTickersPuller(IHttpClientFactory requestService)
    {
        m_RequestService = requestService;
        m_TradingViewUrl = "https://scanner.tradingview.com/america/scan?label-product=underchart-screener-stock";
        m_RequestBody = @"
        {
            ""filter"": [
                {
                    ""left"": ""type"",
                    ""operation"": ""in_range"",
                    ""right"": [
                        ""stock"",
                        ""dr"",
                        ""fund""
                    ]
                },
                {
                    ""left"": ""subtype"",
                    ""operation"": ""in_range"",
                    ""right"": [
                        ""common"",
                        ""foreign-issuer"",
                        """",
                        ""etf"",
                        ""etf,odd"",
                        ""etf,otc"",
                        ""etf,cfd""
                    ]
                },
                {
                    ""left"": ""exchange"",
                    ""operation"": ""in_range"",
                    ""right"": [
                        ""NYSE"",
                        ""NASDAQ"",
                        ""AMEX""
                    ]
                },
                {
                    ""left"": ""change"",
                    ""operation"": ""greater"",
                    ""right"": 0
                },
                {
                    ""left"": ""close"",
                    ""operation"": ""in_range"",
                    ""right"": [
                        2,
                        10000
                    ]
                },
                {
                    ""left"": ""is_primary"",
                    ""operation"": ""equal"",
                    ""right"": true
                },
                {
                    ""left"": ""active_symbol"",
                    ""operation"": ""equal"",
                    ""right"": true
                }
            ],
            ""options"": {
                ""lang"": ""en""
            },
            ""markets"": [
                ""america""
            ],
            ""symbols"": {
                ""query"": {
                    ""types"": []
                },
                ""tickers"": []
            },
            ""columns"": [

            ],
            ""sort"": {
                ""sortBy"": ""change"",
                ""sortOrder"": ""desc""
            },
            ""range"": [
                0,
                100
            ]
            }";
        m_RequestContent = new HttpRequestMessage(HttpMethod.Post, m_TradingViewUrl)
        {
            Content = new StringContent(m_RequestBody, Encoding.UTF8, "application/json")
        };
    }


    public async Task<IEnumerable<string>> ScanTickers(CancellationToken cancellationToken)
    {
        var client = m_RequestService.CreateClient();
        client.AddBrowserUserAgent();
        var result = await client.PostAsync(m_TradingViewUrl, new StringContent(m_RequestBody, Encoding.UTF8, "application/json"), cancellationToken);

        if (!result.IsSuccessStatusCode)
        {
            return [];
        }

        var content = await result.Content.ReadFromJsonAsync<ScannerModel>(cancellationToken);

        if (content is null || content.Data is null)
        {
            return [];
        }

        return [.. content.Data.Select(data => data!.TickerName.Split(":")[1])];
    }

    public Task<IEnumerable<string>> ScanTickers(TickersPullerParameters scannerParams, CancellationToken cancellationToken)
    {
        return ScanTickers(cancellationToken);
    }
}
