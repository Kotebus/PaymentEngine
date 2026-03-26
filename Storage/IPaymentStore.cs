using PaymentEngine.Models;

namespace PaymentEngine.Storage;

public interface IPaymentStore
{
    PaymentRecord? TryGetExisting(string merchantId, string orderId);
    void Store(PaymentRecord record);
}