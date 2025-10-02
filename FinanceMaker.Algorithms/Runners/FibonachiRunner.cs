using System;
using FinanceMaker.Common;
using FinanceMaker.Common.Models.Finance;
using FinanceMaker.Pullers.PricesPullers.Interfaces;

namespace FinanceMaker.Algorithms.Runners;

public class FibonachiRunner : TickerRangeAlgorithmRunnerBase<KeyLevelCandleSticks>
{
    public FibonachiRunner(IPricesPuller pricesPuller) : base(pricesPuller)
    {
    }

    public override Algorithm Algorithm => Algorithm.Fibonachi;

    public override Task<KeyLevelCandleSticks> Run(IEnumerable<FinanceCandleStick> input, CancellationToken cancellationToken)
    {
        if (input is null)
        {
            throw new ArgumentException("Input candles cannot be null or empty.");
        }

        var candles = input.ToList();
        var high = float.MinValue;
        var low = float.MaxValue;

        for (int i = 0; i < candles.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<KeyLevelCandleSticks>(cancellationToken);
            }

            if (candles[i].High > high)
            {
                high = candles[i].High;
            }

            if (candles[i].Low < low)
            {
                low = candles[i].Low;
            }
        }

        var diff = high - low;
        var fibLevels = new float[]
        {
    (float)low,
    (float)(high - 0.236f * diff),
    (float)(high - 0.382f * diff),
    (float)(high - 0.5f * diff),
    (float)(high - 0.618f * diff),
    (float)high
        };

        var result = new KeyLevelCandleSticks(input.Select(_ => new EMACandleStick(_, 0)), fibLevels);


        return Task.FromResult(result);
    }
}
