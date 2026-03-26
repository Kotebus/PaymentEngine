namespace PaymentEngine.Models;

public record PaymentResult(
    PaymentStatus Status,
    string? ProviderReference = null,
    string? ThreeDsUrl = null,
    string? DeclineReason = null,
    string? ErrorMessage = null
);
