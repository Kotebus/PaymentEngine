using System.Collections.Concurrent;
using PaymentEngine.Models;

namespace PaymentEngine.Storage;

public class PaymentStore : IPaymentStore
{
    private readonly ConcurrentDictionary<string, PaymentRecord> _records = new();

    private static string GetKey(string merchantId, string orderId) => $"{merchantId}:{orderId}";

    public PaymentRecord? TryGetExisting(string merchantId, string orderId)
    {
        _records.TryGetValue(GetKey(merchantId, orderId), out var record);
        return record;
    }

    public void Store(PaymentRecord record)
    {
        var key = GetKey(record.MerchantId, record.OrderId);
        if (!_records.TryAdd(key, record))
        {
            throw new InvalidOperationException("Idempotency error: we're trying to add an already existing record " + key);
        }
    }
}
