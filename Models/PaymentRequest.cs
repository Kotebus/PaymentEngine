namespace PaymentEngine.Models;

public record PaymentRequest(
    string MerchantId,
    string OrderId,
    long Amount,
    string Currency,
    string CardToken
);
