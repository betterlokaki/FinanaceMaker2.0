using FinanceMaker.Common;
using FinanceMaker.Common.Extensions;
using FinanceMaker.Common.Models.Finance;
using FinanceMaker.Common.Models.Finance.Enums;
using FinanceMaker.Pullers.PricesPullers.Interfaces;

namespace FinanceMaker.Algorithms;

public sealed class KeyLevelsRunner :
    TickerRangeAlgorithmRunnerBase<KeyLevelCandleSticks>
{
    private int m_Neighbors;
    private float m_Epsilon;

    public KeyLevelsRunner(IPricesPuller pricesPuller) : base(pricesPuller)
    {
        m_Neighbors = 3;
        m_Epsilon = 0.005f;
    }

    public override Algorithm Algorithm => Algorithm.KeyLevels;

    public override Task<KeyLevelCandleSticks> Run(IEnumerable<FinanceCandleStick> input,
                                                   CancellationToken cancellationToken)
    {
        var count = input.GetNonEnumeratedCount();
        var candlesArr = input.ToArray();
        var pivots = new Pivot[count];
        var levels = new List<float>();
        var emaCandles = new EMACandleStick[count];

        for (var i = 0; i < count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("Cancelled on key level detection");
            }
            var pivot = GetPivot(candlesArr, i, m_Neighbors, m_Neighbors);

            pivots[i] = pivot;

            if (pivot == Pivot.High)
            {
                levels.Add((float)candlesArr[i].High);
            }

            if (pivot == Pivot.Low)
            {
                levels.Add((float)candlesArr[i].Low);
            }

            emaCandles[i] = new(candlesArr[i], 0)
            {
                Pivot = pivot
            };
        }
        // We need to return it some how I don't know why
        List<float> distinctedLevels = [];
        var epsilon = m_Epsilon;

        for (int i = 0; i < levels.Count; i++)
        {
            if (!distinctedLevels.Any(level => level + level * epsilon >= levels[i] && levels[i] >= level - level * epsilon))
            {
                distinctedLevels.Add(levels[i]);
            }
        }
        var beforeRemoveSimilar = distinctedLevels.ToArray();
        for (int i = 0; i < distinctedLevels.Count; i++)
        {
            for (int j = i + 1; j < distinctedLevels.Count; j++)
            {
                if (distinctedLevels[j] + distinctedLevels[j] * 3 * epsilon >= distinctedLevels[i] &&
                    distinctedLevels[i] >= distinctedLevels[j] - distinctedLevels[j] * 3 * epsilon)
                {
                    distinctedLevels[i] = (distinctedLevels[i] + distinctedLevels[j]) / 2;
                    distinctedLevels.RemoveAt(j);
                    j--; // Adjust index after removal
                }
            }
        }
        var reuslt = new KeyLevelCandleSticks(emaCandles, distinctedLevels);
        // var newLevels = DetectKeyLevels(input.ToList());
        // reuslt.KeyLevels = [.. newLevels];
        return Task.FromResult(reuslt);
        // return Task.FromResult((IEnumerable<EMACandleStick>)emaCandles);

    }

    /// <summary>
    /// Detects key support and resistance levels from a list of candlesticks.
    /// </summary>
    /// <param name="candles">List of daily candlestick data.</param>
    /// <returns>List of key levels as float values.</returns>
    public List<float> DetectKeyLevels(List<FinanceCandleStick> candles)
    {
        var levels = new List<float>();
        if (candles == null || candles.Count < 5)
            return levels;

        // Calculate average candle size to determine proximity threshold
        float avgRange = candles.Average(c => c.High - c.Low);

        for (int i = 2; i < candles.Count - 2; i++)
        {
            if (IsSupport(candles, i))
            {
                float level = candles[i].Low;
                if (IsFarFromLevel(level, levels, avgRange))
                    levels.Add(level);
            }
            else if (IsResistance(candles, i))
            {
                float level = candles[i].High;
                if (IsFarFromLevel(level, levels, avgRange))
                    levels.Add(level);
            }
        }

        return levels;
    }

    /// <summary>
    /// Determines if the candle at index i is a support level.
    /// </summary>
    private bool IsSupport(List<FinanceCandleStick> candles, int i)
    {
        return candles[i].Low < candles[i - 1].Low &&
               candles[i].Low < candles[i + 1].Low &&
               candles[i + 1].Low < candles[i + 2].Low &&
               candles[i - 1].Low < candles[i - 2].Low;
    }

    /// <summary>
    /// Determines if the candle at index i is a resistance level.
    /// </summary>
    private bool IsResistance(List<FinanceCandleStick> candles, int i)
    {
        return candles[i].High > candles[i - 1].High &&
               candles[i].High > candles[i + 1].High &&
               candles[i + 1].High > candles[i + 2].High &&
               candles[i - 1].High > candles[i - 2].High;
    }

    /// <summary>
    /// Checks if the level is sufficiently far from existing levels.
    /// </summary>
    private bool IsFarFromLevel(float level, List<float> levels, float threshold)
    {
        return levels.All(existingLevel => Math.Abs(level - existingLevel) > threshold);
    }


    /// <summary>
    /// Calculate the pivot of the current candle by the NIO algorithm 
    /// it is just a banch of "ifs"
    /// </summary>
    /// <param name="candles"></param>
    /// <param name="index"></param>
    /// <param name="neighborsRight"></param>
    /// <param name="neighborsLeft"></param>
    /// <returns></returns> <summary>
    private static Pivot GetPivot(FinanceCandleStick[] candles,
                                  int index,
                                  int neighborsRight,
                                  int neighborsLeft)
    {
        if (index - neighborsLeft < 0 || (index + neighborsRight) >= candles.Length)
        {
            return Pivot.Unchanged;
        }

        Pivot pivotLow = Pivot.Low;
        Pivot pivotHigh = Pivot.High;

        for (int i = index - neighborsLeft; i < index + neighborsRight; i++)
        {
            if (candles[index].Low > candles[i].Low)
            {
                pivotLow = Pivot.Unchanged;
            }
            if (candles[index].High < candles[i].High)
            {
                pivotHigh = Pivot.Unchanged;
            }
        }

        if (pivotLow == pivotHigh)
        {
            return Pivot.Unchanged;
        }

        else if (pivotLow == Pivot.Low)
        {
            return pivotLow;
        }
        else if (pivotHigh == Pivot.High)
        {
            return pivotHigh;
        }

        return Pivot.Unchanged;
    }
}
