using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FinanceMaker.Brokers.InteractiveBrokers;

public sealed class IBKRHttpClient
{
    private readonly IBKRConfig _config;
    private readonly HttpClient _httpClient;
    private readonly IBKRAuthenticator _authenticator;

    public IBKRHttpClient(IBKRConfig config, HttpMessageHandler? handler = null)
    {
        _config = config;
        _httpClient = handler != null ? new HttpClient(handler, disposeHandler: false) : new HttpClient();

        // Retry basics
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_config.UserAgent);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));

        _authenticator = new IBKRAuthenticator(config, _httpClient);

        // Initialize brokerage session and accounts upfront like the python client
        InitBrokerageSessionAsync().GetAwaiter().GetResult();
        GetBrokerageAccountsAsync().GetAwaiter().GetResult();
    }

    public async Task<JsonNode> InitBrokerageSessionAsync()
    {
        var endpoint = "/iserver/auth/ssodh/init";
        var payload = JsonNode.Parse(JsonSerializer.Serialize(new { publish = true, compete = true }));
        return await PostAsync(endpoint, payload);
    }

    public async Task<JsonNode> LogoutAsync()
    {
        var endpoint = "/logout";
        return await PostAsync(endpoint, null);
    }

    public async Task<IEnumerable<string>> GetBrokerageAccountsAsync()
    {
        var endpoint = "/iserver/accounts";
        var data = await GetAsync(endpoint);
        var accountsArray = data?["accounts"]?.AsArray();
        return accountsArray?.Select(n => n?.GetValue<string>() ?? string.Empty)
                           .Where(s => !string.IsNullOrEmpty(s))
                           .ToList() ?? new List<string>();
    }

    public async Task<JsonArray> PortfolioAccountsAsync()
    {
        var endpoint = "/portfolio/accounts";
        var response = await GetAsync(endpoint);
        return response?["items"]?.AsArray() ?? new JsonArray();
    }

    public async Task<JsonNode> PortfolioSubaccountsAsync()
    {
        var endpoint = "/portfolio/subaccounts";
        return await GetAsync(endpoint);
    }

    public async Task<JsonNode> PortfolioSubaccountsLargeAsync(int pageNumber = 0)
    {
        var endpoint = "/portfolio/subaccounts2";
        var query = new Dictionary<string, string> { ["page"] = pageNumber.ToString() };
        return await GetAsync(endpoint, null, query);
    }

    public async Task<JsonNode> PortfolioAccountMetadataAsync(string accountId)
    {
        var endpoint = $"/portfolio/{accountId}/meta";
        return await GetAsync(endpoint);
    }

    public async Task<JsonNode> PortfolioAccountAllocationAsync(string accountId)
    {
        var endpoint = $"/portfolio/{accountId}/allocation";
        return await GetAsync(endpoint);
    }

    public async Task<JsonNode> PortfolioAccountPositionsAsync(string accountId)
    {
        var endpoint = $"/portfolio/{accountId}/combo/positions";
        var query = new Dictionary<string, string> { ["nocache"] = "true" };
        return await GetAsync(endpoint, null, query);
    }

    public async Task<JsonNode> PortfolioAllAllocationAsync(IEnumerable<string> accountIds)
    {
        var endpoint = "/portfolio/allocation";
        var payload = JsonNode.Parse(JsonSerializer.Serialize(new { acctIds = accountIds.ToArray() }));
        return await PostAsync(endpoint, payload);
    }

    public async Task<JsonNode> GetPositionsAsync(string accountId, int pageId = 0)
    {
        var endpoint = $"/portfolio/{accountId}/positions/{pageId}";
        return await GetAsync(endpoint);
    }

    public async Task<JsonNode> GetAllPositionsAsync(string accountId, Models.SortingOrder sortingOrder)
    {
        var endpoint = $"/portfolio2/{accountId}/positions";
        var direction = sortingOrder == Models.SortingOrder.Ascending ? "a" : "d";
        var query = new Dictionary<string, string> { ["direction"] = direction, ["sort"] = "position" };
        return await GetAsync(endpoint, null, query);
    }

    public async Task<JsonNode> GetPositionsByContractIdAsync(string accountId, string contractId)
    {
        var endpoint = $"/portfolio/{accountId}/position/{contractId}";
        return await GetAsync(endpoint);
    }

    public async Task<JsonNode> GetPortfolioSummaryAsync(string accountId)
    {
        var endpoint = $"/portfolio/{accountId}/summary";
        return await GetAsync(endpoint);
    }

    public async Task<JsonNode> GetPortfolioLedgerAsync(string accountId)
    {
        var endpoint = $"/portfolio/{accountId}/ledger";
        return await GetAsync(endpoint);
    }

    public async Task<JsonNode> GetPositionInfoByContractIdAsync(string contractId)
    {
        var endpoint = $"/portfolio/positions/{contractId}";
        return await GetAsync(endpoint);
    }

    public async Task<JsonNode> GetAccountsPerformanceAsync(IEnumerable<string> accountIds, Models.Period period)
    {
        var endpoint = "/pa/performance";
        var periodStr = period switch { Models.Period.OneDay => "1D", Models.Period.OneWeek => "7D", Models.Period.MonthToDate => "MTD", Models.Period.OneMonth => "1M", Models.Period.YearToDate => "YTD", Models.Period.OneYear => "1Y", _ => "1D" };
        var payload = JsonNode.Parse(JsonSerializer.Serialize(new { acctIds = accountIds.ToArray(), period = periodStr }));
        return await PostAsync(endpoint, payload);
    }

    public async Task<JsonNode> GetAccountsTransactionsAsync(IEnumerable<string> accountIds, IEnumerable<int> contractIds, Models.BaseCurrency currency = Models.BaseCurrency.USD, int days = 90)
    {
        var endpoint = "/pa/transactions";
        var payload = JsonNode.Parse(JsonSerializer.Serialize(new
        {
            acctIds = accountIds.ToArray(),
            conids = contractIds.ToArray(),
            currency = currency.ToString(),
            days = days
        }));
        return await PostAsync(endpoint, payload);
    }

    public async Task<JsonNode> CreateAlertAsync(string accountId, Models.Alert alert)
    {
        var endpoint = $"/iserver/account/{accountId}/alert";
        var json = JsonSerializer.SerializeToNode(alert);
        return await PostAsync(endpoint, json);
    }

    public async Task<JsonNode> ModifyAlertAsync(string accountId, int alertId, Models.Alert alert)
    {
        var endpoint = $"/iserver/account/{accountId}/alert";
        var json = JsonSerializer.SerializeToNode(alert);
        if (json != null)
        {
            json["order_id"] = alertId;
        }
        return await PostAsync(endpoint, json);
    }

    public async Task<JsonNode> GetAlertListAsync(string accountId)
    {
        var endpoint = $"/iserver/account/{accountId}/alerts";
        return await GetAsync(endpoint);
    }

    public async Task<JsonNode> DeleteAlertAsync(string accountId, string alertId)
    {
        var endpoint = $"/iserver/account/{accountId}/alert/{alertId}";
        return await DeleteAsync(endpoint);
    }

    public async Task<JsonNode> DeleteAllAlertsAsync(string accountId)
    {
        var endpoint = $"/iserver/account/{accountId}/alert/0";
        return await DeleteAsync(endpoint);
    }

    public async Task<JsonNode> SetAlertActivationAsync(string accountId, int alertId, bool alertActive)
    {
        var endpoint = $"/iserver/account/{accountId}/alert/activate";
        var payload = JsonNode.Parse(JsonSerializer.Serialize(new { alertId, alertActive = alertActive ? 1 : 0 }));
        return await PostAsync(endpoint, payload);
    }

    public async Task<JsonNode> GetAlertDetailsAsync(int alertId)
    {
        var endpoint = $"/iserver/account/alert/{alertId}";
        var query = new Dictionary<string, string> { ["type"] = "Q" };
        return await GetAsync(endpoint, null, query);
    }

    public async Task<JsonNode> CreateWatchlistAsync(string watchlistId, string name, IEnumerable<int> contractIds)
    {
        var endpoint = "/iserver/watchlist";
        var rows = contractIds.Select(id => new Dictionary<string, object> { ["C"] = id });
        var payload = JsonNode.Parse(JsonSerializer.Serialize(new { id = watchlistId, name, rows }));
        return await PostAsync(endpoint, payload);
    }

    public async Task<JsonNode> GetAllWatchlistsAsync()
    {
        var endpoint = "/iserver/watchlists";
        var query = new Dictionary<string, string> { ["SC"] = "USER_WATCHLIST" };
        return await GetAsync(endpoint, null, query);
    }

    public async Task<JsonNode> GetWatchlistInfoAsync(string watchlistId)
    {
        var endpoint = "/iserver/watchlist";
        var query = new Dictionary<string, string> { ["id"] = watchlistId };
        return await GetAsync(endpoint, null, query);
    }

    public async Task<JsonNode> DeleteWatchlistAsync(string watchlistId)
    {
        var endpoint = "/iserver/watchlist";
        var query = new Dictionary<string, string> { ["id"] = watchlistId };
        return await DeleteAsync(endpoint, null, query);
    }

    public async Task<JsonNode> GetIServerScannerParamsAsync()
    {
        var endpoint = "/iserver/scanner/params";
        return await GetAsync(endpoint);
    }

    public async Task<JsonNode> IServerMarketScannerAsync(string instrument, string location, string type, IEnumerable<Dictionary<string, object>> filters)
    {
        var endpoint = "/iserver/scanner/run";
        var payload = JsonNode.Parse(JsonSerializer.Serialize(new
        {
            instrument,
            location,
            type,
            filter = filters.ToArray()
        }));
        return await PostAsync(endpoint, payload);
    }

    public async Task<JsonNode> GetHMDSScannerParamsAsync()
    {
        var endpoint = "/hmds/scanner/params";
        return await GetAsync(endpoint);
    }
    public async Task<JsonArray> ReplayOrderAsync(string replyId)
    {

        var endpoint = $"/iserver/reply/{replyId}";
        var payload = JsonNode.Parse(JsonSerializer.Serialize(new { confirmed = true }));
        var result = await PostAsync(endpoint, payload);

        return result.AsArray() ?? new JsonArray();
    }
    public async Task<JsonNode> GetSecurityDefinitionAsync(IEnumerable<int> contractIds)
    {
        var endpoint = "/trsrv/secdef";
        var query = new Dictionary<string, string> { ["conids"] = string.Join(",", contractIds) };
        return await GetAsync(endpoint, null, query);
    }

    public async Task<JsonNode> GetAllContractsAsync(string exchangeId)
    {
        var endpoint = "/trsrv/all-conids";
        var query = new Dictionary<string, string> { ["exchange"] = exchangeId };
        return await GetAsync(endpoint, null, query);
    }

    public async Task<JsonNode> GetContractInfoAsync(int contractId)
    {
        var endpoint = $"/iserver/contract/{contractId}/info";
        return await GetAsync(endpoint);
    }

    public async Task<JsonNode> GetContractInfoAndRulesAsync(int contractId, Models.OrderRule orderRule)
    {
        var endpoint = $"/iserver/contract/{contractId}/info-and-rules";
        var query = new Dictionary<string, string> { ["isBuy"] = (orderRule == Models.OrderRule.Buy).ToString().ToLowerInvariant() };
        return await GetAsync(endpoint, null, query);
    }
    public async Task<JsonNode> GetOrders()
    {
        var endpoint = "/iserver/account/orders";
        return await GetAsync(endpoint);
    }

    public async Task<JsonNode> GetCurrencyPairsAsync(Models.BaseCurrency currency)
    {
        var endpoint = "/iserver/currency/pairs";
        var query = new Dictionary<string, string> { ["currency"] = currency.ToString() };
        return await GetAsync(endpoint, null, query);
    }

    public async Task<JsonNode> GetCurrencyExchangeRateAsync(Models.BaseCurrency targetCurrency, Models.BaseCurrency sourceCurrency)
    {
        var endpoint = "/iserver/exchangerate";
        var query = new Dictionary<string, string> { ["target"] = targetCurrency.ToString(), ["source"] = sourceCurrency.ToString() };
        return await GetAsync(endpoint, null, query);
    }

    public async Task<JsonNode> GetFuturesBySymbolAsync(IEnumerable<string> symbols)
    {
        var endpoint = "/trsrv/futures";
        var query = new Dictionary<string, string> { ["symbols"] = string.Join(",", symbols) };
        return await GetAsync(endpoint, null, query);
    }

    public async Task<JsonNode> GetStocksBySymbolAsync(IEnumerable<string> symbols)
    {
        var endpoint = "/trsrv/stocks";
        var query = new Dictionary<string, string> { ["symbols"] = string.Join(",", symbols) };
        return await GetAsync(endpoint, null, query);
    }

    public async Task<JsonNode> GetLiveMarketDataSnapshotAsync(IEnumerable<int> contractIds, IEnumerable<Models.MarketDataField> fields)
    {
        var endpoint = "/iserver/marketdata/snapshot";
        var query = new Dictionary<string, string>
        {
            ["conids"] = string.Join(",", contractIds),
            ["fields"] = string.Join(",", fields.Select(f => ((int)f).ToString()))
        };
        return await GetAsync(endpoint, null, query);
    }

    private async Task<JsonNode> GetAsync(string endpoint, JsonNode? json = null, Dictionary<string, string>? query = null)
    {
        var method = "GET";
        var url = BuildUrl(endpoint, query);
        var headers = _authenticator.GetHeaders(method, url);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        foreach (var kv in headers) request.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
        if (json != null)
        {
            request.Content = new StringContent(json.ToJsonString(), Encoding.UTF8, "application/json");
        }

        using var response = await _httpClient.SendAsync(request);
        await EnsureSuccessWithDetails(response);
        return await ReadJsonNodeAsync(response);
    }

    private async Task<JsonNode> PostAsync(string endpoint, JsonNode? json = null, Dictionary<string, string>? query = null)
    {
        var method = "POST";
        var url = BuildUrl(endpoint, query);
        var headers = _authenticator.GetHeaders(method, url);

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        foreach (var kv in headers) request.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
        if (json != null)
        {
            request.Content = new StringContent(json.ToJsonString(), Encoding.UTF8, "application/json");
        }

        using var response = await _httpClient.SendAsync(request);
        await EnsureSuccessWithDetails(response);
        return await ReadJsonNodeAsync(response);
    }

    private async Task<JsonNode> DeleteAsync(string endpoint, JsonNode? json = null, Dictionary<string, string>? query = null)
    {
        var method = "DELETE";
        var url = BuildUrl(endpoint, query);
        var headers = _authenticator.GetHeaders(method, url);

        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        foreach (var kv in headers) request.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
        if (json != null)
        {
            request.Content = new StringContent(json.ToJsonString(), Encoding.UTF8, "application/json");
        }

        using var response = await _httpClient.SendAsync(request);
        await EnsureSuccessWithDetails(response);
        return await ReadJsonNodeAsync(response);
    }

    private string BuildUrl(string endpoint, Dictionary<string, string>? query)
    {
        var baseUrl = _config.BaseUrl.TrimEnd('/');
        var path = endpoint.TrimStart('/');
        var url = $"{baseUrl}/{path}";
        if (query is { Count: > 0 })
        {
            var qp = string.Join("&", query.Select(kv => $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));
            url += "?" + qp;
        }
        return url;
    }

    private static async Task EnsureSuccessWithDetails(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var content = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException($"Request failed: {(int)response.StatusCode} {response.StatusCode}, content: {content}");
    }

    private static async Task<JsonNode> ReadJsonNodeAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        Stream finalStream = stream;
        if (response.Content.Headers.ContentEncoding.Contains("gzip"))
        {
            finalStream = new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionMode.Decompress);
        }

        // Parse once and convert to JsonNode
        using var doc = await JsonDocument.ParseAsync(finalStream);
        var jsonString = doc.RootElement.GetRawText();
        return JsonNode.Parse(jsonString) ?? JsonNode.Parse("{}") ?? throw new InvalidOperationException("Failed to parse JSON response");
    }

    public async Task<JsonNode> PlaceOrderAsync(string accountId, JsonNode[] orders)
    {
        var endpoint = $"/iserver/account/{accountId}/orders";
        var payload = JsonNode.Parse(JsonSerializer.Serialize(new { orders }));
        var result = await PostAsync(endpoint, payload);
        return result?.AsArray() ?? new JsonArray();
    }
}


