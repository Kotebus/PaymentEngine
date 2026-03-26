using PaymentEngine.Models;

namespace PaymentEngine.Provider;

public interface IPaymentProviderClient
{
    Task<ProviderCallResult> ChargeAsync(ProviderRequest request, CancellationToken ct);
}