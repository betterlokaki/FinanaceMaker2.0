// Licensed to the .NET Foundation under one or more agreements.

using System.Text.Json;
using System.Text.Json.Serialization;
using FinanceMaker.Common.Models.Finance;

namespace FinanceMaker.Common.Converters.JsonConverters
{
    public class KeyLevelCandleSticksConverter : JsonConverter<KeyLevelCandleSticks>
    {
        public override KeyLevelCandleSticks Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(
            Utf8JsonWriter writer,
            KeyLevelCandleSticks keyLevelCandles,
            JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, keyLevelCandles.KeyLevels, options);
        }
    }
}
