using PaymentEngine.Models;

namespace PaymentEngine.Validation;

public interface IPaymentRequestValidator
{
    IList<string> Validate(PaymentRequest request);
}