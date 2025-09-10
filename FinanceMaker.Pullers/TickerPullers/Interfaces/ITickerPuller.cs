namespace FinanceMaker.Pullers.TickerPullers.Interfaces;

public interface ITickerPuller
{
    Task<IEnumerable<string>> ScanTickers(CancellationToken cancellationToken);
}


