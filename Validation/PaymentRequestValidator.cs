using PaymentEngine.Models;

namespace PaymentEngine.Validation;

public class PaymentRequestValidator : IPaymentRequestValidator
{
    //We're using a third-party NuGet package with currency codes. It might be worth transferring it to our system and
    //eliminating this unnecessary dependency.
    private static readonly HashSet<string> ValidCurrencies =
        ISO._4217.CurrencyCodesResolver.Codes
            .Select(c => c.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    
    public IList<string> Validate(PaymentRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.MerchantId))
            errors.Add("MerchantId is required.");

        if (string.IsNullOrWhiteSpace(request.OrderId))
            errors.Add("OrderId is required.");

        if (request.Amount <= 0)
            errors.Add("Amount must be greater than zero.");

        if (string.IsNullOrWhiteSpace(request.Currency))
            errors.Add("Currency is required.");
        else if (request.Currency is not { Length: 3 } || !ValidCurrencies.Contains(request.Currency))
            errors.Add("Currency must be a 3-letter ISO code.");

        if (string.IsNullOrWhiteSpace(request.CardToken))
            errors.Add("CardToken is required.");

        return errors;
    }
}
