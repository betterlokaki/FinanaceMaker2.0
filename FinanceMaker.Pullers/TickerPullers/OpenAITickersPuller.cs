using System;
using FinanceMaker.Common.Models.Pullers;
using FinanceMaker.Pullers.TickerPullers.Interfaces;

namespace FinanceMaker.Pullers.TickerPullers;

public class OpenAITickersPuller : IParamtizedTickersPuller
{
    private readonly IHttpClientFactory m_RequestService;

    public OpenAITickersPuller(IHttpClientFactory requestService)
    {
        m_RequestService = requestService;
    }

    public Task<IEnumerable<string>> ScanTickers(TickersPullerParameters scannerParams,
                                                 CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
