using PaymentEngine.Models;

namespace PaymentEngine.Services;

public interface IPaymentService
{
    Task<PaymentResult> CallAsync(PaymentRequest request, CancellationToken ct = default);
}