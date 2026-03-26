using System.Text.Json.Serialization;

namespace PaymentEngine.Models;

public record ProviderRequest(
    [property: JsonPropertyName("amount")] long Amount,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("card_token")] string CardToken
);
