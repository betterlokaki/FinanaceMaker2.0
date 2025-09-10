using System.Text.Json.Serialization;

namespace FinanceMaker.Common.Models.Interactive;

public class IBKRAuthResponse
{
    [JsonPropertyName("authenticated")]
    public bool Authenticated { get; set; }
}
