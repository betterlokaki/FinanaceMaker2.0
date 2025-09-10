using FinanceMaker.Common;
using FinanceMaker.Common.Models.Finance;
using FinanceMaker.Common.Resolvers.Interfaces;

namespace FinanceMaker.Algorithms;

// TODO: Change this arc to be like the ideas (you didn't code for a long time broooo)
public interface IAlgorithmRunner<TInput>: IResolveable<TInput> 
            where TInput : class

{
    AlgorithmType AlgorithmType{ get; }

    Task<IEnumerable<FinanceCandleStick>> Run(TInput input, CancellationToken cancellationToken);
}
