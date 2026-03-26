namespace PaymentEngine.Models;

public record PaymentRecord(
    string MerchantId,
    string OrderId,
    long Amount,
    string Currency,
    PaymentResult Result,
    DateTimeOffset CreatedAt
);
