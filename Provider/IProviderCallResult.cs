using PaymentEngine.Models;

namespace PaymentEngine.Provider;

public interface IProviderCallResult
{
    int? HttpStatusCode { get; }
    ProviderResponse? Response { get; }
    string? RawBody { get; }
    Exception? Exception { get; }
}