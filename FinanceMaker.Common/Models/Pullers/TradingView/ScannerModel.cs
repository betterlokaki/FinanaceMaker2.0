using System.Text.Json.Serialization;

namespace FinanceMaker.Common.Models.Pullers.TradingView;

public class ScannerModel
{
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }
    [JsonPropertyName("data")]
    public List<Datum> Data { get; set; } = [];

}


public class Datum
{
    [JsonPropertyName("s")]
    public string TickerName { get; set; } = string.Empty;
}
