using FinanceMaker.Common.Models.Ideas.IdeaOutputs;
using FinanceMaker.Common.Models.Trades.Enums;
using FinanceMaker.Common.Models.Trades.Trader;
using FinanceMaker.Publisher.Orders.Trader.Interfaces;
using FinanceMaker.Trades.Publisher.Orders.Trades.Interfaces;

namespace FinanceMaker.Publisher.Orders.Trader.Abstracts;

public abstract class BrokerBase<T> : IBroker
    where T : GeneralOutputIdea
{
    public abstract TraderType Type { get; }
    public Task<ITrade> BrokerTrade(GeneralOutputIdea idea, CancellationToken cancellationToken)
    {
        if (idea is not T realIdea)
        {
            throw new ArgumentException($"Trader got {idea.GetType()} but need {typeof(T)}");
        }

        return TradeInternal(realIdea, cancellationToken);
    }

    protected abstract Task<ITrade> TradeInternal(T idea, CancellationToken cancellationToken);
    public abstract Task<Position> GetClientPosition(CancellationToken cancellationToken);
    public abstract Task CancelTrade(ITrade trade, CancellationToken cancellationToken);
}
