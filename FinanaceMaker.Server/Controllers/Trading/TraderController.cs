using FinanceMaker.Common.Models.Ideas.IdeaInputs;
using FinanceMaker.Common.Models.Ideas.IdeaOutputs;
using FinanceMaker.Common.Models.Pullers;
using FinanceMaker.Ideas.Ideas;
using FinanceMaker.Publisher.Orders.Trader.Interfaces;
using FinanceMaker.Publisher.Traders.Interfaces;
using FinanceMaker.Trades.Publisher.Orders.Trades.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FinanaceMaker.Server.Controllers.Trading
{
    [Route("api/[controller]")]
    [ApiController]
    public class TraderController : ControllerBase
    {
        private readonly OverNightBreakout m_Idea;
        private readonly IBroker m_Broker;
        private readonly ITrader m_Trader;

        public TraderController(OverNightBreakout ideal, IBroker broker, ITrader trader)
        {
            m_Idea = ideal;
            m_Broker = broker;
            m_Trader = trader;
        }

        [HttpGet]
        public Task<IEnumerable<ITrade>> TradeOvernight(CancellationToken cancellationToken)
        {
            return ShouldMoveToAnotherClass(cancellationToken);
        }
        [HttpGet, Route(nameof(TradeOvernightForHours))]
        public async Task<IEnumerable<ITrade>> TradeOvernightForHours(CancellationToken cancellationToken)
        {
            var o = await ShouldMoveToAnotherClass(cancellationToken);
            var timer = new System.Timers.Timer(TimeSpan.FromHours(1));

            timer.Elapsed += async (sender, e) =>
            {
                var now = DateTime.Now;
                if (now.TimeOfDay >= TimeSpan.FromHours(16) + TimeSpan.FromMinutes(30) && now.TimeOfDay.Hours <= 23)
                {
                    await ShouldMoveToAnotherClass(cancellationToken);

                }
            };

            timer.Start();

            return o;
        }
        [HttpGet, Route(nameof(ActiveateTrader))]
        public async Task<IActionResult> ActiveateTrader(CancellationToken cancellation)
        {
            await m_Trader.Trade(cancellation);

            return Ok();
        }
        private async Task<IEnumerable<ITrade>> ShouldMoveToAnotherClass(CancellationToken cancellationToken)
        {
            var input = new TechnicalIdeaInput()
            {
                TechnicalParams = TickersPullerParameters.BestBuyer
            };

            var result = (await m_Idea.CreateIdea(input, cancellationToken)).ToList();

            input = new TechnicalIdeaInput()
            {
                TechnicalParams = TickersPullerParameters.BestSellers
            };
            var result2 = await m_Idea.CreateIdea(input, cancellationToken);

            result.AddRange(result2);

            var tradesResult = new List<ITrade>();
            var position = await m_Broker.GetClientPosition(cancellationToken);
            var openedPositoins = position.OpenedPositions;
            var moneyForEachTrade = position.BuyingPower / result.Count;
            var actualResult = result;

            foreach (var idea in actualResult)
            {
                if (idea is EntryExitOutputIdea entryExitOutputIdea)
                {
                    entryExitOutputIdea.Quantity = (int)(moneyForEachTrade / entryExitOutputIdea.Entry);
                }
                var trade = await m_Broker.BrokerTrade(idea, cancellationToken);

                tradesResult.Add(trade);
            }
            return tradesResult;
        }
    }
}
