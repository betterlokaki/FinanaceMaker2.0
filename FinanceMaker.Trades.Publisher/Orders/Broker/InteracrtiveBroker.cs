using System;
using System.Linq;
using System.Text.Json.Nodes;
using FinanceMaker.Brokers.InteractiveBrokers;
using FinanceMaker.Brokers.InteractiveBrokers.Models;
using FinanceMaker.Common.Models.Ideas.Enums;
using FinanceMaker.Common.Models.Ideas.IdeaOutputs;
using FinanceMaker.Common.Models.Trades.Enums;
using FinanceMaker.Common.Models.Trades.Trader;
using FinanceMaker.Publisher.Orders.Trader.Abstracts;
using FinanceMaker.Publisher.Orders.Trades;
using FinanceMaker.Trades.Publisher.Orders.Trades.Interfaces;

namespace FinanceMaker.Publisher.Orders.Broker;

public enum OrderDirection
{
    Long,
    Short
}


public class InteracrtiveBroker : BrokerBase<EntryExitOutputIdea>
{
    private readonly IBKRHttpClient _client;

    public InteracrtiveBroker(IBKRConfig config)
    {
        _client = new IBKRHttpClient(config);
    }

    public override TraderType Type => TraderType.EntryExit;

    public override async Task CancelTrade(ITrade trade, CancellationToken cancellationToken)
    {
        var accountId = await GetAccountId();
        await _client.DeleteAlertAsync(accountId, trade.TradeId.ToString());
        await trade.Cancel(cancellationToken);
    }

    public override async Task<Position> GetClientPosition(CancellationToken cancellationToken)
    {
        var accountId = await GetAccountId();
        var positions = await _client.GetAllPositionsAsync(accountId, SortingOrder.Ascending);
        var summary = await _client.GetPortfolioSummaryAsync(accountId);
        var orders = await _client.GetOrders();
        var orderActual = orders!["orders"]!.AsArray().Select(_ => _!["ticker"]!.ToString()).Distinct().ToArray();
        return new Position
        {
            BuyingPower = summary?["buyingpower"]!["amount"]?.GetValue<float>() ?? 0f,
            OpenedPositions = positions?.AsArray()?.Select(p => p!["description"]!.ToString() ?? string.Empty)
                                     .Where(s => !string.IsNullOrEmpty(s))
                                     .ToArray() ?? Array.Empty<string>(),
            Orders = orderActual
        };
    }

    protected override async Task<ITrade> TradeInternal(EntryExitOutputIdea idea, CancellationToken cancellationToken)
    {
        var accountId = await GetAccountId();

        // Get contract details using market scanner
        var scannerResponse = await _client.IServerMarketScannerAsync(
            "STK", // instrument
            "STK.US.MAJOR", // location
            "TOP_PERC_GAIN", // type
            new[] { new Dictionary<string, object> { ["symbol"] = idea.Ticker } }
        );

        // Extract conid from scanner response
        var conid = scannerResponse?["contracts"]?.AsArray()?[0]?["con_id"]?.GetValue<int>();
        if (conid is null)
        {
            throw new InvalidOperationException($"Could not find contract ID for ticker {idea.Ticker}");
        }

        // Create a unique order ID
        var orderId = Guid.NewGuid();
        var customerOrderId = orderId.ToString();

        // Create the order object
        var order = new JsonObject
        {
            ["acctId"] = accountId,
            ["conid"] = conid,
            ["orderType"] = "LMT",
            ["side"] = idea.Trade == IdeaTradeType.Long ? "BUY" : "SELL",
            ["ticker"] = idea.Ticker,
            ["tif"] = "DAY",
            ["quantity"] = idea.Quantity,
            ["price"] = idea.Entry,
            ["cOID"] = customerOrderId,
            ["outsideRTH"] = true,
            ["manualIndicator"] = false,
            ["extOperator"] = "FinanceMaker2.0",
            ["secType"] = $"{conid}@STK",
            ["conidex"] = $"{conid}SMART",
            ["listingExchange"] = "NASDAQ",
        };

        // Create stop loss order
        var stopLossOrder = new JsonObject
        {
            ["acctId"] = accountId,
            ["conid"] = conid,
            ["orderType"] = "STP",
            ["side"] = idea.Trade == IdeaTradeType.Long ? "SELL" : "BUY",
            ["ticker"] = idea.Ticker,
            ["tif"] = "GTC",
            ["quantity"] = idea.Quantity,
            ["price"] = idea.Stoploss,
            ["parentId"] = conid,
            ["outsideRTH"] = true,
            ["manualIndicator"] = false,
            ["extOperator"] = "FinanceMaker2.0",
            ["secType"] = $"{conid}@STK",
            ["conidex"] = $"{conid}SMART",
            ["listingExchange"] = "NASDAQ",
        };

        // Create take profit order
        var takeProfitOrder = new JsonObject
        {
            ["acctId"] = accountId,
            ["conid"] = conid,
            ["orderType"] = "LMT",
            ["side"] = idea.Trade == IdeaTradeType.Long ? "SELL" : "BUY",
            ["ticker"] = idea.Ticker,
            ["tif"] = "GTC",
            ["quantity"] = idea.Quantity,
            ["price"] = idea.Exit,
            ["parentId"] = conid,
            ["outsideRTH"] = true,
            ["manualIndicator"] = false,
            ["extOperator"] = "FinanceMaker2.0",

            ["secType"] = $"{conid}@STK",
            ["conidex"] = $"{conid}SMART",

            ["listingExchange"] = "NASDAQ",
        };

        // Place all orders (entry order with OCO stop loss and take profit)
        var orderResponse = await _client.PlaceOrderAsync(accountId, new[] { order, stopLossOrder, takeProfitOrder });
        var realOrderResponse = orderResponse[0];
        var orderIdResponse = realOrderResponse?["id"]!.GetValue<string>() ?? string.Empty;
        // Check for success response
        if (string.IsNullOrEmpty(orderIdResponse))
        {
            if (realOrderResponse?["error"] != null)
            {
                throw new InvalidOperationException($"Order placement failed: {realOrderResponse["error"]}");
            }
            throw new InvalidOperationException("Order placement failed with unknown error");
        }
        // Log the order ID
        var replayId = orderIdResponse;
        JsonArray? replayData = null;
        string? orderStatus = null;
        int maxAttempts = 4;
        int attempt = 0;

        while (attempt < maxAttempts)
        {
            var data = await _client.ReplayOrderAsync(replayId);
            replayData = data?.AsArray();
            var firstOrder = replayData?.FirstOrDefault();
            orderStatus = firstOrder?["order_status"]?.GetValue<string>();

            if (string.Equals(orderStatus, "Submitted", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            // Check for failure status or error
            if (firstOrder?["order_status"] != null &&
            (string.Equals(orderStatus, "Failed", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(orderStatus, "Rejected", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(orderStatus, "Cancelled", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Order replay failed: {orderStatus}");
            }

            // Try to get next replay id if available
            var nextReplayId = firstOrder?["id"]?.GetValue<string>();
            if (string.IsNullOrEmpty(nextReplayId))
            {
                throw new InvalidOperationException("Order replay confirmation failed with unknown error");
            }
            replayId = nextReplayId;
            attempt++;
        }

        if (!string.Equals(orderStatus, "Submitted", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Order replay did not reach Submitted status");
        }
        return new Trade(idea, orderId, true);
    }

    private async Task<string> GetAccountId()
    {
        var accounts = await _client.GetBrokerageAccountsAsync();
        return accounts.FirstOrDefault() ?? throw new InvalidOperationException("No accounts found");
    }
}
