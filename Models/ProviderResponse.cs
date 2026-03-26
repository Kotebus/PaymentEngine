using System.Text.Json.Serialization;

namespace PaymentEngine.Models;

public class ProviderResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("system_order_ref")]
    public string? SystemOrderRef { get; set; }

    [JsonPropertyName("amount")]
    public long? Amount { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("threeds_url")]
    public string? ThreeDsUrl { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}
