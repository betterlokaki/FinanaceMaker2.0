using FinanceMaker.Common;
using FinanceMaker.Common.Extensions;
using FinanceMaker.Common.Models.Finance;
using FinanceMaker.Common.Models.Finance.Enums;
using FinanceMaker.Pullers.PricesPullers.Interfaces;

namespace FinanceMaker.Algorithms;

public class BreakOutDetectionRunner :
 TickerRangeAlgorithmRunnerBase<IEnumerable<EMACandleStick>>
{
    private readonly int m_BackCandles;
    private readonly int m_Window;
    private readonly int m_NumOfCandlesToBeConsideredAsBreakout;
    private readonly KeyLevelsRunner m_KeyLevelsRunner;

    public override Algorithm Algorithm => Algorithm.BreakoutDetection;

    public BreakOutDetectionRunner(IPricesPuller pricesPuller,
                                   KeyLevelsRunner keyLevelsRunner) : base(pricesPuller)
    {
        m_BackCandles = 30;
        m_Window = 30;
        m_NumOfCandlesToBeConsideredAsBreakout = 10;
        m_KeyLevelsRunner = keyLevelsRunner;
    }


    public override async Task<IEnumerable<EMACandleStick>> Run(IEnumerable<FinanceCandleStick> input, CancellationToken cancellationToken)
    {
        if (input is not IEnumerable<EMACandleStick> financeCandleSticks)
        {
            financeCandleSticks = await m_KeyLevelsRunner.Run(input, cancellationToken);
        }

        var arr = financeCandleSticks.ToArray();
        var emaCnadles = new EMACandleStick[arr.Length];
        var breakouts = new TrendTypes[arr.Length];

        for (int i = 0; i < arr.Length; i++)

        {
            var breakout = IsItBreakoutCandle(arr,
                                              i,
                                              m_BackCandles,
                                              m_Window,
                                              m_NumOfCandlesToBeConsideredAsBreakout);
            breakouts[i] = breakout;
            arr[i].BreakThrough = breakout;
            emaCnadles[i] = new EMACandleStick(arr[i], 0)
            {
                BreakThrough = breakout
            };
        }

        return emaCnadles;
    }

    private static TrendTypes IsItBreakoutCandle(EMACandleStick[] financeCandleSticks,
                                    int index,
                                    int backCandles,
                                    int window,
                                    int numOfCandlesToBeConsideredAsBreakout)
    {
        if (index <= (backCandles + window) || (index + window > financeCandleSticks.Length)) return TrendTypes.NoChange;


        var smallArray = financeCandleSticks.Skip(index - backCandles - window)
                                            .Take(financeCandleSticks.Length - index - window)
                                            .ToArray();

        var highs = smallArray.Where(_ => _.Pivot == Pivot.High)
                              .Select(_ => _.High)
                              .TakeLast(numOfCandlesToBeConsideredAsBreakout);
        var lows = smallArray.Where(_ => _.Pivot == Pivot.Low)
                              .Select(_ => _.Low)
                              .TakeLast(numOfCandlesToBeConsideredAsBreakout);

        var levelBreak = TrendTypes.NoChange;
        var zoneWidth = 0.0002f;

        if (lows.GetNonEnumeratedCount() == numOfCandlesToBeConsideredAsBreakout)
        {
            var supportCondition = true;
            var lowAverage = lows.Average();

            foreach (var low in lows)
            {
                if (Math.Abs(lowAverage - low) > zoneWidth)
                {
                    supportCondition = false;
                    break;
                }
            }

            if (supportCondition && (lowAverage - financeCandleSticks[index].Close) > zoneWidth)
            {
                levelBreak = TrendTypes.Berish;
            }
        }

        if (highs.GetNonEnumeratedCount() == numOfCandlesToBeConsideredAsBreakout)
        {
            var resistanceCondition = true;
            var highAverage = highs.Average();

            foreach (var high in highs)
            {
                if (Math.Abs(high - highAverage) > zoneWidth)
                {
                    resistanceCondition = false;
                    break;
                }
            }

            if (resistanceCondition && (financeCandleSticks[index].Close - highAverage) > zoneWidth)
            {
                levelBreak = TrendTypes.Bulish;
            }
        }

        return levelBreak;
    }
}


