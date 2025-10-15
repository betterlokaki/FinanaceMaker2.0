using System;
using FinanceMaker.Publisher.Traders.Interfaces;
using FinanceMaker.Pullers.TickerPullers;

namespace FinanceMaker.Publisher.Traders;
/// <summary>
/// This trader pulls the stocks from finviz which have a gap up in the last 4 hours
/// and then trade them based on the range algorithm. but with much much higher risk: reward ratio.
/// </summary>
public class SwingTrader : ITrader
{
    private readonly FourHourGapTickersPullers m_TickersPullers;

    public SwingTrader(FourHourGapTickersPullers tickersPullers)
    {
        m_TickersPullers = tickersPullers;
    }
    public Task Trade(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
