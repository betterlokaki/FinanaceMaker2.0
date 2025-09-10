using FinanceMaker.Common.Models.Ideas.IdeaOutputs;
using FinanceMaker.Trades.Publisher.Orders.Trades.Interfaces;

namespace FinanceMaker.Publisher.Orders.Trades;

public class Trade : ITrade
{
    public bool IsActive { get; set; }

    public bool IsFinished { get; private set; }
    public GeneralOutputIdea Idea { get; }

    public Guid TradeId { get; }
    public static Trade Empty => new Trade(GeneralOutputIdea.Empty, Guid.NewGuid(), true);
    public Trade(GeneralOutputIdea idea, Guid tradeId, bool isFinished)
    {
        Idea = idea;
        TradeId = tradeId;
        IsFinished = isFinished;
    }
    public Task Cancel(CancellationToken cancellationToken)
    {
        IsActive = false;
        IsFinished = true;

        return Task.CompletedTask;
    }
}
