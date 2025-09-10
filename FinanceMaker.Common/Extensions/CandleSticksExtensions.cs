using FinanceMaker.Common.Models.Finance;

namespace FinanceMaker.Common.Extensions;

public static class CandleSticksExtensions
{
    /// <summary>
    /// Caluclates the closest keylevels to last check wheter the trade is in a risk or not 
    /// if the presntage is low it means that we are very close to some keylevel
    /// if not it means we are very far away from therefore consider again whether it worth the risk
    /// </summary>
    /// <param name="keyLevelCandleSticks">
    /// Keylevels candle sticks whichi you got from the KeyLevelsRunner
    /// </param>
    /// <param name="maxPresentage">
    /// maximum presetage which the algo will quit if nothing is found (for less calculation)
    /// </param>
    /// <param name="numberOfKeyLevels">
    /// number of output key levels
    /// </param> 
    /// <returns>
    /// The closest levels 
    /// </returns>
    ///  <summary>
    public static (IEnumerable<double> closestKeyLevels, double precentage)
    GetClosestToLastKeyLevels(this KeyLevelCandleSticks keyLevelCandleSticks,
                               int maxPresentage = 100,
                               int numberOfKeyLevels = 1)
    {
        // The precents of the closing keylevels
        var precentage = 1;
        var keyLevels = keyLevelCandleSticks.KeyLevels;
        var lastCandleStick = keyLevelCandleSticks.Last();

        for (var i = 0; i < keyLevels.Length && i < maxPresentage; i++)
        {
            var closest = keyLevels.Where(x => x + (x * precentage / 100) >= lastCandleStick.Close &&
                                           x - (x * precentage / 100) <= lastCandleStick.Close);

            if (closest.GetNonEnumeratedCount() >= numberOfKeyLevels)
            {
                var data = closest.OrderDescending()
                                  .Take(numberOfKeyLevels)
                                  .Select(_ => (double)_);

                return (data, precentage);
            }

            precentage++;
        }

        return ([], precentage);
    }
}
