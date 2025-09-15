using Alpaca.Markets;
using FinanceMaker.Common.Models.Ideas.IdeaOutputs;
using FinanceMaker.Common.Models.Trades.Enums;
using FinanceMaker.Common.Models.Trades.Trader;
using FinanceMaker.Publisher.Extensions;
using FinanceMaker.Publisher.Orders.Trader.Abstracts;
using FinanceMaker.Publisher.Orders.Trades;
using ITrade = FinanceMaker.Trades.Publisher.Orders.Trades.Interfaces.ITrade;

namespace FinanceMaker.Publisher.Orders.Trader;

public class AlpacaBroker : BrokerBase<EntryExitOutputIdea>
{
    // I really need to create both secrets and configs 
    const string API_KEY = "PKH61BCHIWNB11A588E2";
    const string API_SECRET = "kAHcVCqGcOLlwo0ZEbBI60WVtzoXnTvq8YPT9roz";
    const string ENDPOIONT_URL = "https://paper-api.alpaca.markets/v2";

    private readonly IAlpacaTradingClient m_Client;

    public override TraderType Type => TraderType.EntryExit | TraderType.StopLoss;

    public AlpacaBroker()
    {
        m_Client = Environments.Paper
                .GetAlpacaTradingClient(new SecretKey(API_KEY, API_SECRET));
    }
    protected override async Task<ITrade> TradeInternal(EntryExitOutputIdea idea, CancellationToken cancellationToken)
    {
        if (idea.Quantity < 1)
        {
            throw new Exception("Bro what's worng with you and math? you can't buy less than 1 stock");
        }
        var request = idea.ConvertToAlpacaRequest();
        try
        {
            var order = await m_Client.PostOrderAsync(request, cancellationToken);
            var trade = new Trade(idea, order.OrderId, true);

            if (cancellationToken.IsCancellationRequested)
            {
                await CancelTrade(trade, CancellationToken.None);
            }

            return trade;
        }

        catch (Exception)
        {
            return Trade.Empty;
        }

    }

    public override async Task CancelTrade(ITrade trade, CancellationToken cancellationToken)
    {
        await trade.Cancel(cancellationToken);

        await m_Client.CancelOrderAsync(trade.TradeId, cancellationToken);

        if (trade.Idea is EntryExitOutputIdea tradeIdea)
        {
            var order = tradeIdea.ConvertToAlpacaCancelTrade();
            var canceled = await m_Client.PostOrderAsync(order);
        }
    }

    public override async Task<Position> GetClientPosition(CancellationToken cancellationToken)
    {
        // Remoev this code
        var accountData = await m_Client.GetAccountAsync(cancellationToken);
        var accountPosition = await m_Client.ListPositionsAsync(cancellationToken);
        var accountOpenedOrders = await m_Client.ListOrdersAsync(new ListOrdersRequest(), cancellationToken);
        float buyingPower = accountData.BuyingPower is null ? 0f : (float)accountData.BuyingPower.Value;
        decimal? p = accountOpenedOrders.Where(_ => _.OrderStatus != OrderStatus.New).Select(_ => _.LimitPrice * _.Quantity).Sum();
        p ??= 0;

        var poposition = new Position()
        {
            BuyingPower = buyingPower - (float)p,
            OpenedPositions = accountPosition.Select(_ => _.Symbol).ToArray(),
            Orders = accountOpenedOrders.Where(_
            => _.OrderStatus.HasFlag(OrderStatus.New)
            || _.OrderStatus.HasFlag(OrderStatus.PartialFill)
            || _.OrderStatus.HasFlag(OrderStatus.PartiallyFilled)
            || _.OrderStatus.HasFlag(OrderStatus.PendingNew)

                ).Select(_ => _.Symbol).ToArray()
        };

        return poposition;
    }
}
