using FinanceMaker.Common;
using FinanceMaker.Common.Extensions;
using FinanceMaker.Common.Models.Finance;
using FinanceMaker.Pullers.PricesPullers.Interfaces;

namespace FinanceMaker.Algorithms;

public sealed class EMARunner : TickerRangeAlgorithmRunnerBase<IEnumerable<EMACandleStick>>
{
    private readonly int m_Period;
    public override Algorithm Algorithm => Algorithm.EMA;

    public EMARunner(IPricesPuller pricesPuller) : base(pricesPuller)
    {
        // I would probably would add a model that I will inject to the container
        // make it dynamic some (change the input)
        m_Period = 10;
    }


    public override Task<IEnumerable<EMACandleStick>> Run(
        IEnumerable<FinanceCandleStick> input, CancellationToken token)
    {
        var count = input.GetNonEnumeratedCount();
        var emaValues = new float[count];
        var eMACandleSticks = new EMACandleStick[count];
        var prices = input.Select(_ => _.Close)
                          .ToArray();

        var multiplier = 2.0f / (m_Period + 1);
        var ema = prices[0]; // Start with the first price

        emaValues[0] = ema; // Add the first EMA value

        for (int i = 1; i < prices.Length; i++)
        {
            ema = ((prices[i] - ema) * multiplier) + ema;
            emaValues[i] = ema;
            eMACandleSticks[i] = new EMACandleStick(input.ElementAt(i), ema);

            if (token.IsCancellationRequested)
            {
                throw new OperationCanceledException("Cancelled while running the EMA allgorithm ");
            }
        }


        return Task.FromResult((IEnumerable<EMACandleStick>)eMACandleSticks);
    }
}
