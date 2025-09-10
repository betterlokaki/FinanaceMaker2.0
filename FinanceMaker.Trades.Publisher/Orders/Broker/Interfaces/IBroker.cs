using FinanceMaker.Common.Models.Ideas.IdeaOutputs;
using FinanceMaker.Common.Models.Trades.Enums;
using FinanceMaker.Common.Models.Trades.Trader;
using FinanceMaker.Trades.Publisher.Orders.Trades.Interfaces;

namespace FinanceMaker.Publisher.Orders.Trader.Interfaces;

/// <summary>
/// Manging the trading, trader could be static dynamic and so on..
/// </summary>
public interface IBroker
{
    /// <summary>
    /// Type of the trader mainly effects the activity of it 
    /// </summary>
    /// <value></value>
    TraderType Type { get; }
    /// <summary>
    /// Starts new trades according to a specific idea
    /// </summary>
    /// <param name="idea">The idea which the trader will follow</param>
    /// <param name="cancellationToken">Cancel the initialize</param>
    /// <returns>Trade which related to this trade</returns>
    Task<ITrade> BrokerTrade(GeneralOutputIdea idea, CancellationToken cancellationToken);
    Task<Position> GetClientPosition(CancellationToken cancellationToken);
    Task CancelTrade(ITrade trade, CancellationToken cancellationToken);
}
