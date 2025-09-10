// using System;
// using FinanceMaker.Common.Models.Finance.Enums;
// using QuantConnect;

using System.Text.Json.Serialization;
using FinanceMaker.Common.Converters.JsonConverters;

namespace FinanceMaker.Common.Models.Finance;
[JsonConverter(typeof(KeyLevelCandleSticksConverter))]
public class KeyLevelCandleSticks : List<EMACandleStick>
{
    public float[] KeyLevels { get; set; }

    public KeyLevelCandleSticks(IEnumerable<EMACandleStick> eMACandleSticks,
                                IEnumerable<float> keyLevels)
        : base(eMACandleSticks)
    {
        KeyLevels = keyLevels.ToArray();
    }
}

