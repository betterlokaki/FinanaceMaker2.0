using Alpaca.Markets;
using FinanceMaker.Common.Models.Ideas.Enums;
using FinanceMaker.Common.Models.Ideas.IdeaOutputs;

namespace FinanceMaker.Publisher.Extensions;

public static class AlpacaExtensions
{
    public static NewOrderRequest ConvertToAlpacaRequest(this EntryExitOutputIdea idea)
    {
        var orderSide = idea.Trade == IdeaTradeType.Long ? OrderSide.Buy : OrderSide.Sell;
        var request = new NewOrderRequest(idea.Ticker,
                                           idea.Quantity,
                                           orderSide,
                                           OrderType.Limit,
                                           TimeInForce.Gtc)
        {
            LimitPrice = (decimal)Math.Round(idea.Entry, 2),
            TakeProfitLimitPrice = (decimal)Math.Round(idea.Exit, 2),
            OrderClass = OrderClass.Bracket,
            StopLossStopPrice = (decimal)Math.Round(idea.Stoploss, 2),
            //ExtendedHours = true // Don't do it for now it throws exception
        };

        return request;
    }

    public static NewOrderRequest ConvertToAlpacaCancelTrade(this EntryExitOutputIdea idea)
    {
        // we want the opposite here
        var orderSide = idea.Trade == IdeaTradeType.Long ? OrderSide.Sell : OrderSide.Buy;
        var request = new NewOrderRequest(idea.Ticker,
                                           idea.Quantity,
                                           orderSide,
                                           OrderType.Market,
                                           TimeInForce.Day);


        return request;
    }
}
