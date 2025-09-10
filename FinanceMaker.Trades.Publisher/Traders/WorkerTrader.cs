using FinanceMaker.Common;
using FinanceMaker.Common.Extensions;
using FinanceMaker.Common.Models.Ideas.IdeaInputs;
using FinanceMaker.Common.Models.Ideas.IdeaOutputs;
using FinanceMaker.Ideas.Ideas.Abstracts;
using FinanceMaker.Publisher.Orders.Trader.Interfaces;
using FinanceMaker.Publisher.Traders.Interfaces;
using FinanceMaker.Pullers.PricesPullers.Interfaces;
using FinanceMaker.Trades.Publisher.Orders.Trades.Interfaces;

namespace FinanceMaker.Publisher.Traders;

public class WorkerTrader : ITrader
{
    private const int MAX_OPENED_TRADES = 10;
    private readonly Queue<EntryExitOutputIdea> m_IdeasToActive;
    private readonly IBroker m_Broker;
    private readonly IdeaBase<TechnicalIdeaInput, EntryExitOutputIdea> m_IdeasPuller;
    private readonly List<TechnicalIdeaInput> m_IdeasParams;
    private readonly List<ITrade> m_OpenedTrades;
    private readonly IPricesPuller m_PricesPuller;
    private readonly MarketStatus m_IsMarketOpen;
    public WorkerTrader(IBroker broker,
                         IdeaBase<TechnicalIdeaInput, EntryExitOutputIdea> ideasPuller,
                         IPricesPuller puller,
                         MarketStatus isMarketOpen)
    {
        m_IdeasToActive = [];
        m_Broker = broker;
        m_IdeasPuller = ideasPuller;

        m_IdeasParams = [
            TechnicalIdeaInput.BestBuyers,
            TechnicalIdeaInput.BestSellers,
        ];
        m_PricesPuller = puller;
        m_OpenedTrades = [];
        m_IsMarketOpen = isMarketOpen;
    }
    public async Task Trade(CancellationToken cancellationToken)
    {
        var isMarketOpen = await m_IsMarketOpen.IsMarketOpenAsync(cancellationToken);

        if (!isMarketOpen)
        {
            return;
        }
        await GetIdeas(cancellationToken);
        var copy = m_IdeasToActive.ToArray();

        foreach (var idea in copy)
        {
            m_IdeasToActive.TryDequeue(out var _);
            var currentPosition = await m_Broker.GetClientPosition(cancellationToken);

            if (currentPosition.OpenedPositions.GetNonEnumeratedCount() >= MAX_OPENED_TRADES)
            {

                continue;
            }

            // No need to trade a stock we've already traded
            // TODO: Do it both before the caculations
            // Close and trade if it finds better (not so important)
            if (currentPosition.OpenedPositions.Contains(idea.Ticker)
                || currentPosition.Orders.Contains(idea.Ticker) || m_OpenedTrades.Any(_ => _.Idea.Ticker == idea.Ticker))
            {
                continue;
            }
            if (idea.Quantity == 0)
            {
                idea.Quantity = (int)(currentPosition.BuyingPower / (MAX_OPENED_TRADES + currentPosition.OpenedPositions.GetNonEnumeratedCount() + currentPosition.Orders.GetNonEnumeratedCount()) / idea.Entry);
            }
            var trade = await m_Broker.BrokerTrade(idea, cancellationToken);

            m_OpenedTrades.Add(trade);
        }
    }
    public async Task GetIdeas(CancellationToken cancellationToken)
    {
        var relevantIdeas = await GetNewIdeas(cancellationToken);
        var openedIdeas = m_OpenedTrades.Select(_ => _.Idea)
                                        .OfType<EntryExitOutputIdea>();
        relevantIdeas.AddRange(openedIdeas);
        var relevantnaceAndProfitIdeas = new List<(int, EntryExitOutputIdea)>();
        List<Task> ideasTasks = [];
        foreach (var idea in relevantIdeas)
        {
            if (openedIdeas.Any(_ => _.Ticker == idea.Ticker)) continue;
            var ideaTask = Task.Run(async () =>
            {
                var currentPrice = await m_PricesPuller.GetTickerPrices(PricesPullerParameters.GetTodayParams(idea.Ticker),
                                                                        cancellationToken);
                if (currentPrice is null || !currentPrice.Any()) return;
                var currentPriceClose = currentPrice.Last().Close;
                var lower = Math.Min(currentPriceClose, idea.Entry);
                var higher = Math.Max(currentPriceClose, idea.Entry);

                var relevant = 100 * (int)(lower / higher);

                relevantnaceAndProfitIdeas.Add((relevant + (int)idea.ProfitPressent, idea));

            });

            ideasTasks.Add(ideaTask);
        }
        await Task.WhenAll(ideasTasks);
        var ideasToPublish = relevantnaceAndProfitIdeas.OrderByDescending(_ => _.Item1)
                                                       .Take(MAX_OPENED_TRADES)
                                                       .Select(_ => _.Item2)
                                                       .ToArray();
        var tradesToCancel = relevantIdeas.Except(ideasToPublish)
                                          .Where(_ => m_OpenedTrades.Any(a => a.Idea == _))
                                          .Select(_ => m_OpenedTrades.FirstOrDefault(_ => _.Idea == _))
                                          .ToArray();

        foreach (var idea in ideasToPublish)
        {
            m_IdeasToActive.Enqueue(idea);
        }
    }

    private async Task<List<EntryExitOutputIdea>> GetNewIdeas(CancellationToken cancellationToken)
    {
        var ideasTasks = new List<Task<IEnumerable<GeneralOutputIdea>>>();

        foreach (var idea in m_IdeasParams)
        {
            var ideaTask = m_IdeasPuller.CreateIdea(idea, cancellationToken);
            ideasTasks.Add(ideaTask);
        }

        var ideasResult = await Task.WhenAll(ideasTasks);
        var relevantIdeas = ideasResult.SelectMany(_ => _)
                                       .OfType<EntryExitOutputIdea>()
                                       .ToList();

        return relevantIdeas;
    }
}
