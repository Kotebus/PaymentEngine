namespace PaymentEngine.Models;

public enum PaymentStatus
{
    Approved,
    ThreeDsRequired,
    Declined,
    ProviderError,
    ValidationError,
    NetworkError
}
