using FinanceMaker.Common;
using FinanceMaker.Common.Models.Finance;
using FinanceMaker.Pullers.PricesPullers.Interfaces;

namespace FinanceMaker.Algorithms;

public abstract class TickerRangeAlgorithmRunnerBase<T> : IAlgorithmRunner<RangeAlgorithmInput>
    where T : IEnumerable<FinanceCandleStick>
{
    private readonly IPricesPuller m_PricesPuller;

    public AlgorithmType AlgorithmType => AlgorithmType.Prices;
    public abstract Algorithm Algorithm { get; }

    public TickerRangeAlgorithmRunnerBase(IPricesPuller pricesPuller)
    {
        m_PricesPuller = pricesPuller;
    }

    public async Task<T> Run(RangeAlgorithmInput input, CancellationToken cancellationToken)
    {
        IEnumerable<FinanceCandleStick> prices = await m_PricesPuller.GetTickerPrices(input, cancellationToken);

        var result = await Run(prices, cancellationToken);

        return result;
    }

    public abstract Task<T> Run(IEnumerable<FinanceCandleStick> input, CancellationToken cancellationToken);

    public virtual bool IsRelevant(RangeAlgorithmInput args)
    {
        return args.Algorithm == Algorithm;
    }
    async Task<IEnumerable<FinanceCandleStick>> IAlgorithmRunner<RangeAlgorithmInput>.Run(RangeAlgorithmInput input, CancellationToken cancellationToken)
    {
        var result = await Run(input, cancellationToken);

        if (result is T actualResult)
        {
            return actualResult;
        }

        throw new Exception($"Bad result {result}");
    }
}
