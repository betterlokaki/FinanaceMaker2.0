using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

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

    public async Task<Dictionary<string, object>> InitBrokerageSessionAsync()
    {
        var endpoint = "/iserver/auth/ssodh/init";
        var payload = new Dictionary<string, object> { ["publish"] = true, ["compete"] = true };
        return await PostAsync(endpoint, payload);
    }

    public async Task<Dictionary<string, object>> LogoutAsync()
    {
        var endpoint = "/logout";
        return await PostAsync(endpoint, null);
    }

    public async Task<Dictionary<string, object>> GetBrokerageAccountsAsync()
    {
        var endpoint = "/iserver/accounts";
        return await GetAsync(endpoint);
    }

    public async Task<List<Dictionary<string, object>>> PortfolioAccountsAsync()
    {
        var endpoint = "/portfolio/accounts";
        var p = await GetAsync(endpoint);
        return (List<Dictionary<string, object>>)p["items"];
    }

    // Portfolio endpoints
    public async Task<Dictionary<string, object>> PortfolioSubaccountsAsync()
    {
        var endpoint = "/portfolio/subaccounts";
        return await GetAsync(endpoint);
    }

    public async Task<Dictionary<string, object>> PortfolioSubaccountsLargeAsync(int pageNumber = 0)
    {
        var endpoint = "/portfolio/subaccounts2";
        var query = new Dictionary<string, string> { ["page"] = pageNumber.ToString() };
        return await GetAsync(endpoint, null, query);
    }

    public async Task<Dictionary<string, object>> PortfolioAccountMetadataAsync(string accountId)
    {
        var endpoint = $"/portfolio/{accountId}/meta";
        return await GetAsync(endpoint);
    }

    public async Task<Dictionary<string, object>> PortfolioAccountAllocationAsync(string accountId)
    {
        var endpoint = $"/portfolio/{accountId}/allocation";
        return await GetAsync(endpoint);
    }

    public async Task<Dictionary<string, object>> PortfolioAccountPositionsAsync(string accountId)
    {
        var endpoint = $"/portfolio/{accountId}/combo/positions";
        var query = new Dictionary<string, string> { ["nocache"] = "true" };
        return await GetAsync(endpoint, null, query);
    }

    public async Task<Dictionary<string, object>> PortfolioAllAllocationAsync(IEnumerable<string> accountIds)
    {
        var endpoint = "/portfolio/allocation";
        var body = new Dictionary<string, object> { ["acctIds"] = accountIds.ToArray() };
        return await PostAsync(endpoint, body);
    }

    public async Task<Dictionary<string, object>> GetPositionsAsync(string accountId, int pageId = 0)
    {
        var endpoint = $"/portfolio/{accountId}/positions/{pageId}";
        return await GetAsync(endpoint);
    }

    public async Task<Dictionary<string, object>> GetAllPositionsAsync(string accountId, Models.SortingOrder sortingOrder)
    {
        var endpoint = $"/portfolio2/{accountId}/positions";
        var direction = sortingOrder == Models.SortingOrder.Ascending ? "a" : "d";
        var query = new Dictionary<string, string> { ["direction"] = direction, ["sort"] = "position" };
        return await GetAsync(endpoint, null, query);
    }

    public async Task<Dictionary<string, object>> GetPositionsByContractIdAsync(string accountId, string contractId)
    {
        var endpoint = $"/portfolio/{accountId}/position/{contractId}";
        return await GetAsync(endpoint);
    }

    public async Task<Dictionary<string, object>> InvalidateBackendPortfolioCacheAsync(string accountId)
    {
        var endpoint = $"/portfolio/{accountId}/positions/invalidate";
        return await PostAsync(endpoint, null);
    }

    public async Task<Dictionary<string, object>> GetPortfolioSummaryAsync(string accountId)
    {
        var endpoint = $"/portfolio/{accountId}/summary";
        return await GetAsync(endpoint);
    }

    public async Task<Dictionary<string, object>> GetPortfolioLedgerAsync(string accountId)
    {
        var endpoint = $"/portfolio/{accountId}/ledger";
        return await GetAsync(endpoint);
    }

    public async Task<Dictionary<string, object>> GetPositionInfoByContractIdAsync(string contractId)
    {
        var endpoint = $"/portfolio/positions/{contractId}";
        return await GetAsync(endpoint);
    }

    // Portfolio Analyst endpoints
    public async Task<Dictionary<string, object>> GetAccountsPerformanceAsync(IEnumerable<string> accountIds, Models.Period period)
    {
        var endpoint = "/pa/performance";
        var periodStr = period switch { Models.Period.OneDay => "1D", Models.Period.OneWeek => "7D", Models.Period.MonthToDate => "MTD", Models.Period.OneMonth => "1M", Models.Period.YearToDate => "YTD", Models.Period.OneYear => "1Y", _ => "1D" };
        var body = new Dictionary<string, object> { ["acctIds"] = accountIds.ToArray(), ["period"] = periodStr };
        return await PostAsync(endpoint, body);
    }

    public async Task<Dictionary<string, object>> GetAccountsTransactionsAsync(IEnumerable<string> accountIds, IEnumerable<int> contractIds, Models.BaseCurrency currency = Models.BaseCurrency.USD, int days = 90)
    {
        var endpoint = "/pa/transactions";
        var body = new Dictionary<string, object>
        {
            ["acctIds"] = accountIds.ToArray(),
            ["conids"] = contractIds.ToArray(),
            ["currency"] = currency.ToString(),
            ["days"] = days,
        };
        return await PostAsync(endpoint, body);
    }

    // Alerts endpoints
    public async Task<Dictionary<string, object>> CreateAlertAsync(string accountId, Models.Alert alert)
    {
        var endpoint = $"/iserver/account/{accountId}/alert";
        var body = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(System.Text.Json.JsonSerializer.Serialize(alert))!;
        return await PostAsync(endpoint, body);
    }

    public async Task<Dictionary<string, object>> ModifyAlertAsync(string accountId, int alertId, Models.Alert alert)
    {
        var endpoint = $"/iserver/account/{accountId}/alert";
        var body = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(System.Text.Json.JsonSerializer.Serialize(alert))!;
        body["order_id"] = alertId;
        return await PostAsync(endpoint, body);
    }

    public async Task<Dictionary<string, object>> GetAlertListAsync(string accountId)
    {
        var endpoint = $"/iserver/account/{accountId}/alerts";
        return await GetAsync(endpoint);
    }

    public async Task<Dictionary<string, object>> DeleteAlertAsync(string accountId, string alertId)
    {
        var endpoint = $"/iserver/account/{accountId}/alert/{alertId}";
        return await DeleteAsync(endpoint);
    }

    public async Task<Dictionary<string, object>> DeleteAllAlertsAsync(string accountId)
    {
        var endpoint = $"/iserver/account/{accountId}/alert/0";
        return await DeleteAsync(endpoint);
    }

    public async Task<Dictionary<string, object>> SetAlertActivationAsync(string accountId, int alertId, bool alertActive)
    {
        var endpoint = $"/iserver/account/{accountId}/alert/activate";
        var body = new Dictionary<string, object> { ["alertId"] = alertId, ["alertActive"] = alertActive ? 1 : 0 };
        return await PostAsync(endpoint, body);
    }

    public async Task<Dictionary<string, object>> GetAlertDetailsAsync(int alertId)
    {
        var endpoint = $"/iserver/account/alert/{alertId}";
        var query = new Dictionary<string, string> { ["type"] = "Q" };
        return await GetAsync(endpoint, null, query);
    }

    // Watchlists endpoints
    public async Task<Dictionary<string, object>> CreateWatchlistAsync(string watchlistId, string name, IEnumerable<int> contractIds)
    {
        var endpoint = "/iserver/watchlist";
        var rows = contractIds.Select(id => new Dictionary<string, object> { ["C"] = id }).ToArray();
        var body = new Dictionary<string, object> { ["id"] = watchlistId, ["name"] = name, ["rows"] = rows };
        return await PostAsync(endpoint, body);
    }

    public async Task<Dictionary<string, object>> GetAllWatchlistsAsync()
    {
        var endpoint = "/iserver/watchlists";
        var query = new Dictionary<string, string> { ["SC"] = "USER_WATCHLIST" };
        return await GetAsync(endpoint, null, query);
    }

    public async Task<Dictionary<string, object>> GetWatchlistInfoAsync(string watchlistId)
    {
        var endpoint = "/iserver/watchlist";
        var query = new Dictionary<string, string> { ["id"] = watchlistId };
        return await GetAsync(endpoint, null, query);
    }

    public async Task<Dictionary<string, object>> DeleteWatchlistAsync(string watchlistId)
    {
        var endpoint = "/iserver/watchlist";
        var query = new Dictionary<string, string> { ["id"] = watchlistId };
        return await DeleteAsync(endpoint, null, query);
    }

    // Scanner endpoints
    public async Task<Dictionary<string, object>> GetIServerScannerParamsAsync()
    {
        var endpoint = "/iserver/scanner/params";
        return await GetAsync(endpoint);
    }

    public async Task<Dictionary<string, object>> IServerMarketScannerAsync(string instrument, string location, string type, IEnumerable<Dictionary<string, object>> filters)
    {
        var endpoint = "/iserver/scanner/run";
        var body = new Dictionary<string, object> { ["instrument"] = instrument, ["location"] = location, ["type"] = type, ["filter"] = filters.ToArray() };
        return await PostAsync(endpoint, body);
    }

    public async Task<Dictionary<string, object>> GetHMDSScannerParamsAsync()
    {
        var endpoint = "/hmds/scanner/params";
        return await GetAsync(endpoint);
    }

    // Contracts/Trsrv endpoints
    public async Task<Dictionary<string, object>> GetSecurityDefinitionAsync(IEnumerable<int> contractIds)
    {
        var endpoint = "/trsrv/secdef";
        var query = new Dictionary<string, string> { ["conids"] = string.Join(",", contractIds) };
        return await GetAsync(endpoint, null, query);
    }

    public async Task<Dictionary<string, object>> GetAllContractsAsync(string exchangeId)
    {
        var endpoint = "/trsrv/all-conids";
        var query = new Dictionary<string, string> { ["exchange"] = exchangeId };
        return await GetAsync(endpoint, null, query);
    }

    public async Task<Dictionary<string, object>> GetContractInfoAsync(int contractId)
    {
        var endpoint = $"/iserver/contract/{contractId}/info";
        return await GetAsync(endpoint);
    }

    public async Task<Dictionary<string, object>> GetContractInfoAndRulesAsync(int contractId, Models.OrderRule orderRule)
    {
        var endpoint = $"/iserver/contract/{contractId}/info-and-rules";
        var query = new Dictionary<string, string> { ["isBuy"] = (orderRule == Models.OrderRule.Buy).ToString().ToLowerInvariant() };
        return await GetAsync(endpoint, null, query);
    }

    public async Task<Dictionary<string, object>> GetCurrencyPairsAsync(Models.BaseCurrency currency)
    {
        var endpoint = "/iserver/currency/pairs";
        var query = new Dictionary<string, string> { ["currency"] = currency.ToString() };
        return await GetAsync(endpoint, null, query);
    }

    public async Task<Dictionary<string, object>> GetCurrencyExchangeRateAsync(Models.BaseCurrency targetCurrency, Models.BaseCurrency sourceCurrency)
    {
        var endpoint = "/iserver/exchangerate";
        var query = new Dictionary<string, string> { ["target"] = targetCurrency.ToString(), ["source"] = sourceCurrency.ToString() };
        return await GetAsync(endpoint, null, query);
    }

    public async Task<Dictionary<string, object>> GetFuturesBySymbolAsync(IEnumerable<string> symbols)
    {
        var endpoint = "/trsrv/futures";
        var query = new Dictionary<string, string> { ["symbols"] = string.Join(",", symbols) };
        return await GetAsync(endpoint, null, query);
    }

    public async Task<Dictionary<string, object>> GetStocksBySymbolAsync(IEnumerable<string> symbols)
    {
        var endpoint = "/trsrv/stocks";
        var query = new Dictionary<string, string> { ["symbols"] = string.Join(",", symbols) };
        return await GetAsync(endpoint, null, query);
    }

    // Market Data
    public async Task<Dictionary<string, object>> GetLiveMarketDataSnapshotAsync(IEnumerable<int> contractIds, IEnumerable<Models.MarketDataField> fields)
    {
        var endpoint = "/iserver/marketdata/snapshot";
        var query = new Dictionary<string, string>
        {
            ["conids"] = string.Join(",", contractIds),
            ["fields"] = string.Join(",", fields.Select(f => ((int)f).ToString())),
        };
        return await GetAsync(endpoint, null, query);
    }

    private async Task<Dictionary<string, object>> GetAsync(string endpoint, Dictionary<string, object>? json = null, Dictionary<string, string>? query = null)
    {
        var method = "GET";
        var url = BuildUrl(endpoint, query);
        var headers = _authenticator.GetHeaders(method, url);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        foreach (var kv in headers) request.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
        if (json != null)
        {
            var body = System.Text.Json.JsonSerializer.Serialize(json);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        using var response = await _httpClient.SendAsync(request);
        await EnsureSuccessWithDetails(response);
        return await ReadJsonDictionaryAsync(response);
    }

    private async Task<Dictionary<string, object>> PostAsync(string endpoint, Dictionary<string, object>? json = null, Dictionary<string, string>? query = null)
    {
        var method = "POST";
        var url = BuildUrl(endpoint, query);
        var headers = _authenticator.GetHeaders(method, url);

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        foreach (var kv in headers) request.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
        if (json != null)
        {
            var body = System.Text.Json.JsonSerializer.Serialize(json);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        using var response = await _httpClient.SendAsync(request);
        await EnsureSuccessWithDetails(response);
        return await ReadJsonDictionaryAsync(response);
    }

    private async Task<Dictionary<string, object>> DeleteAsync(string endpoint, Dictionary<string, object>? json = null, Dictionary<string, string>? query = null)
    {
        var method = "DELETE";
        var url = BuildUrl(endpoint, query);
        var headers = _authenticator.GetHeaders(method, url);

        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        foreach (var kv in headers) request.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
        if (json != null)
        {
            var body = System.Text.Json.JsonSerializer.Serialize(json);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        using var response = await _httpClient.SendAsync(request);
        await EnsureSuccessWithDetails(response);
        return await ReadJsonDictionaryAsync(response);
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

    private static async Task<Dictionary<string, object>> ReadJsonDictionaryAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        Stream finalStream = stream;
        if (response.Content.Headers.ContentEncoding.Contains("gzip"))
        {
            finalStream = new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionMode.Decompress);
        }

        // Parse once and inspect root type
        var doc = await JsonDocument.ParseAsync(finalStream);
        if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(doc.RootElement.GetRawText());
            return dict ?? new Dictionary<string, object>();
        }
        else if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            var list = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(doc.RootElement.GetRawText());
            return new Dictionary<string, object> { ["items"] = list ?? new List<Dictionary<string, object>>() };
        }
        // Fallback: empty dictionary
        return new Dictionary<string, object>();
    }
}


