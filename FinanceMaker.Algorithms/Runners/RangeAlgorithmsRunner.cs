using FinanceMaker.Common;
using FinanceMaker.Common.Models.Finance;
using FinanceMaker.Common.Resolvers.Abstracts;

namespace FinanceMaker.Algorithms;

public class RangeAlgorithmsRunner : ResolverBase<IAlgorithmRunner<RangeAlgorithmInput>, RangeAlgorithmInput>
{
    public RangeAlgorithmsRunner(IEnumerable<IAlgorithmRunner<RangeAlgorithmInput>> logicsToResolve) : base(logicsToResolve)
    {
    }

    public async Task<IEnumerable<TOutput>> Run<TOutput>(RangeAlgorithmInput rangeAlgorithmInput,
                                                         CancellationToken cancellationToken)
        where TOutput : FinanceCandleStick
    {
        var algoRunner = Resolve(rangeAlgorithmInput);
        var algoResult = await algoRunner.Run(rangeAlgorithmInput, cancellationToken);

        return algoResult is IEnumerable<TOutput> algoResultOutput ?
               algoResultOutput : [];
    }
}
