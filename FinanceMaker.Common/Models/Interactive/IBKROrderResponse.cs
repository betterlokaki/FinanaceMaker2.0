using System.Text.Json.Serialization;

namespace FinanceMaker.Common.Models.Interactive;

public class IBKROrderResponse
{
    [JsonPropertyName("order_id")]
    public string OrderId { get; set; } = string.Empty;

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}
