using FinanceMaker.Common.Models.Ideas.IdeaOutputs;

namespace FinanceMaker.Trades.Publisher.Orders.Trades.Interfaces;

public interface ITrade
{
    /// <summary>
    /// Whether this trader is active (mainly relevant when it is a dynamic trader)
    /// </summary>
    /// <value></value>
    bool IsActive { get; }
    /// <summary>
    /// Whether the trader finished is job, (it doesn't mean it closess all its positions)
    /// It does mean that the rest of the job now is left for the broker, (all trades are submitted)
    /// </summary>
    /// <value></value>
    bool IsFinished { get; }
    /// <summary>
    /// The idea which the trade is related to, probably for future orders
    /// </summary>
    /// <value></value>
    GeneralOutputIdea Idea { get; }

    Guid TradeId { get; }
    /// <summary>
    /// Cancel the trade, close all its open opsitions and cancelling its continuation
    /// </summary>
    /// <param name="cancellationToken">cancel the cancellation</param>
    /// <returns></returns>
    Task Cancel(CancellationToken cancellationToken);
}
