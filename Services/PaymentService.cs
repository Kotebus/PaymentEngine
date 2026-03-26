using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using PaymentEngine.Models;
using PaymentEngine.Provider;
using PaymentEngine.Storage;
using PaymentEngine.Validation;

namespace PaymentEngine.Services;

/// <summary>
/// Orchestrates payment processing: validates requests, deduplicates concurrent calls,
/// charges via the provider, and caches terminal results for idempotency.
/// </summary>
public class PaymentService(
    IPaymentProviderClient providerClient,
    IPaymentStore store,
    IPaymentRequestValidator validator,
    ILogger<PaymentService> logger,
    IResponseInterpreter? responseInterpreter = null)
    : IPaymentService
{
    private readonly IPaymentProviderClient _providerClient = providerClient ?? throw new ArgumentNullException(nameof(providerClient));
    private readonly IPaymentStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly IPaymentRequestValidator _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    private readonly ILogger<PaymentService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IResponseInterpreter _responseInterpreter = responseInterpreter ?? new DefaultResponseInterpreter(logger);

    private static readonly HashSet<PaymentStatus> TerminalStatuses =
    [
        PaymentStatus.Approved,
        PaymentStatus.Declined,
        PaymentStatus.ThreeDsRequired
    ];
    
    private static string GetKey(string merchantId, string orderId) => $"{merchantId}:{orderId}";
    
    private readonly ConcurrentDictionary<string, Lazy<Task<PaymentResult>>> _inFlightRequests = new();
    
    public async Task<PaymentResult> CallAsync(PaymentRequest request, CancellationToken ct)
    {
        // 1. Validate
        var errors = _validator.Validate(request);
        if (errors.Count > 0)
        {
            var message = string.Join("; ", errors);
            _logger.LogWarning("Validation failed for MerchantId={MerchantId}, OrderId={OrderId}: {Errors}",
                request.MerchantId, request.OrderId, message);
            
            return new PaymentResult(PaymentStatus.ValidationError, ErrorMessage: message);
        }

        var key = GetKey(request.MerchantId, request.OrderId);
        
        // Idempotency check
        var cached = _store.TryGetExisting(request.MerchantId, request.OrderId);
        if (cached is not null)
        {
            _logger.LogInformation(
                "Returning cached result for MerchantId={MerchantId}, OrderId={OrderId}, Status={Status}",
                request.MerchantId, request.OrderId, cached.Result.Status);

            return cached.Result;
        }

        var lazy = _inFlightRequests.GetOrAdd(
            key, _ => new Lazy<Task<PaymentResult>>(
                () => LoadAndCacheAsync(request, ct),
                LazyThreadSafetyMode.ExecutionAndPublication
                ));

        var result = await AwaitAndCleanupAsync(key, lazy, ct);
        return result;
    }

    private async Task<PaymentResult> AwaitAndCleanupAsync(
        string key, 
        Lazy<Task<PaymentResult>> lazy, 
        CancellationToken ct)
    {
        try
        {
            return await lazy.Value.WaitAsync(ct);
        }
        finally
        {
            _inFlightRequests.TryRemove(key, out _);
        }
    }
    
    private async Task<PaymentResult> LoadAndCacheAsync(
        PaymentRequest request,
        CancellationToken ct)
    {
        // Call provider
        var providerRequest = new ProviderRequest(request.Amount, request.Currency, request.CardToken);
        var callResult = await _providerClient.ChargeAsync(providerRequest, ct);
        
        // Interpret response
        var result = _responseInterpreter.InterpretResponse(callResult, request);
        
        // Store terminal states only
        if (!IsTerminalStatus(result.Status)) return result;
        
        var record = new PaymentRecord(
            request.MerchantId,
            request.OrderId,
            request.Amount,
            request.Currency,
            result,
            DateTimeOffset.UtcNow);
        
        _store.Store(record);
        
        _logger.LogInformation(
            "Stored payment result: MerchantId={MerchantId}, OrderId={OrderId}, Status={Status}, ProviderRef={ProviderRef}",
            request.MerchantId, request.OrderId, result.Status, result.ProviderReference);
        
        return result;
    }

    private static bool IsTerminalStatus(PaymentStatus status) =>
        TerminalStatuses.Contains(status);
}
