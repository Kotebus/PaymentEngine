using PaymentEngine.Models;

namespace PaymentEngine.Provider;

public record ProviderCallResult(
    int? HttpStatusCode,
    ProviderResponse? Response,
    string? RawBody,
    Exception? Exception
) : IProviderCallResult;
