using System.Text.Json.Serialization;

namespace FinanceMaker.Common.Models.Interactive;

public class IBKRContract
{
    [JsonPropertyName("conid")]
    public string ConId { get; set; } = string.Empty;

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("secType")]
    public string SecurityType { get; set; } = string.Empty;

    [JsonPropertyName("exchange")]
    public string Exchange { get; set; } = string.Empty;
}
