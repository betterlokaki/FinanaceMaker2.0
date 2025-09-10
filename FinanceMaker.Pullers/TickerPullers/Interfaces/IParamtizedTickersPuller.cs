using FinanceMaker.Common.Models.Pullers;

namespace FinanceMaker.Pullers.TickerPullers.Interfaces
{
    public interface IParamtizedTickersPuller
    {
        Task<IEnumerable<string>> ScanTickers(TickersPullerParameters scannerParams, CancellationToken cancellationToken);
    }
}

