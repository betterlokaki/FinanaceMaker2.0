using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Alpaca.Markets;
using FinanceMaker.Common.Models.Ideas.Enums;
using FinanceMaker.Common.Models.Ideas.IdeaOutputs;
using FinanceMaker.Common.Models.Interactive;
using FinanceMaker.Common.Models.Trades.Enums;
using FinanceMaker.Common.Models.Trades.Trader;
using FinanceMaker.Publisher.Orders.Trader.Abstracts;
using FinanceMaker.Publisher.Orders.Trader.Interfaces;
using FinanceMaker.Publisher.Orders.Trades;
using IBApi;
using ITrade = FinanceMaker.Trades.Publisher.Orders.Trades.Interfaces.ITrade;

namespace FinanceMaker.Publisher.Orders.Broker;

public class IBKRBroker : BrokerrBase<EntryExitOutputIdea>
{
    private readonly IHttpClientFactory m_HttpClientFactory;
    private readonly HttpClientHandler m_Handler;
    private readonly string m_BaseUrl;
    private readonly JsonSerializerOptions m_JsonOptions;
    private readonly IBKRClient _ibkrClient;

    public override TraderType Type => TraderType.EntryExit | TraderType.StopLoss;

    public IBKRBroker(IHttpClientFactory httpClientFactory, IBKRClient ibkrClient)
    {
        m_HttpClientFactory = httpClientFactory;
        _ibkrClient = ibkrClient;
        m_Handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        // The base URL should point to your local Client Portal Gateway instance
        m_BaseUrl = "https://localhost:5001/v1/api";
        m_JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        ibkrClient = new IBKRClient();
        _ibkrClient.Connect("127.0.0.1", 4002, 0);
    }

    private void ConfigureClientHeaders(HttpClient client)
    {
        client.DefaultRequestHeaders.Add("User-Agent", "FinanceMaker/1.0");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        client.DefaultRequestHeaders.Add("Host", "api.ibkr.com");
    }

    protected override async Task<ITrade> TradeInternal(EntryExitOutputIdea idea, CancellationToken cancellationToken)
    {
        _ibkrClient.Connect("127.0.0.1", 4002, 0);
        var contract = new Contract
        {
            Symbol = idea.Ticker,
            SecType = "STK",
            Exchange = "SMART",
            Currency = "USD"
        };

        var entryOrder = new Order
        {
            Action = idea.Trade == IdeaTradeType.Long ? "BUY" : "SELL",
            OrderType = "LMT",
            TotalQuantity = idea.Quantity,
            LmtPrice = Math.Round(idea.Entry, 2),
            Tif = "GTC"
        };

        var takeProfitOrder = new Order
        {
            Action = idea.Trade == IdeaTradeType.Long ? "SELL" : "BUY",
            OrderType = "LMT",
            TotalQuantity = idea.Quantity,
            LmtPrice = Math.Round(idea.Exit, 2),
            Tif = "GTC"
        };

        var stopLossOrder = new Order
        {
            Action = idea.Trade == IdeaTradeType.Long ? "SELL" : "BUY",
            OrderType = "STP",
            TotalQuantity = idea.Quantity,
            AuxPrice = Math.Round(idea.Stoploss, 2),


            Tif = "GTC"
        };
        int id = _ibkrClient.GetNextOrderId();

        _ibkrClient.PlaceBracketOrder(id, contract, entryOrder, takeProfitOrder, stopLossOrder);
        await Task.Delay(5_000, cancellationToken); // Wait for the orders to be placed

        return new Trade(idea, Guid.NewGuid(), true);
    }

    private async Task<string> GetContractId(string symbol, CancellationToken cancellationToken)
    {
        var client = m_HttpClientFactory.CreateClient("IBKR");
        ConfigureClientHeaders(client);

        var response = await client.GetAsync($"{m_BaseUrl}/iserver/secdef/search?symbol={Uri.EscapeDataString(symbol)}", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"Contract search failed: {errorContent}");
        }

        var results = await response.Content.ReadFromJsonAsync<List<IBKRContract>>(m_JsonOptions, cancellationToken);

        if (results?.Count > 0)
        {
            return results[0].ConId;
        }

        throw new Exception($"Could not find contract ID for symbol: {symbol}");
    }

    public override async Task<Position> GetClientPosition(CancellationToken cancellationToken)
    {
        _ibkrClient.Connect("127.0.0.1", 4002, 0);
        _ibkrClient.RequestAccountSummary();
        _ibkrClient.RequestCurrentPositions();
        _ibkrClient.RequestOpenOrders();
        await Task.Delay(4_000, cancellationToken); // Wait for the data to be populated
        var positions = _ibkrClient.GetCurrentPositions();
        var buyingPower = _ibkrClient.GetBuyingPower();
        var openOrders = _ibkrClient.GetOpenOrders();
        return new Position
        {
            BuyingPower = (float)buyingPower / 1,
            OpenedPositions = positions.Where(p => p.AvgPrice > 0).Select(p => p.Symbol).ToArray(),
            Orders = openOrders.Where(_ => _.Status == "Submitted").Select(o => o.Symbol).ToArray()
        };
    }

    public override async Task CancelTrade(ITrade trade, CancellationToken cancellationToken)
    {
        _ibkrClient.Connect("127.0.0.1", 4002, 0);
        var client = m_HttpClientFactory.CreateClient("IBKR");
        // client.DefaultRequestHeaders.Add("X-IB-Session", m_SessionId);

        await client.DeleteAsync($"{m_BaseUrl}/iserver/account/order/{trade.TradeId}", cancellationToken);
        await trade.Cancel(cancellationToken);
    }
}
