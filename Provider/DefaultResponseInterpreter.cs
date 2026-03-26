using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PaymentEngine.Constants;
using PaymentEngine.Models;
using PaymentEngine.Services;

namespace PaymentEngine.Provider;

/// <summary>
/// Default implementation of <see cref="IResponseInterpreter"/> that maps provider HTTP responses
/// to <see cref="PaymentResult"/>.
/// </summary>
/// <remarks>
/// Based on <see href="https://docs.stripe.com/api/errors">Stripe API error codes</see>.
/// </remarks>
public class DefaultResponseInterpreter(ILogger<IPaymentService> logger) : IResponseInterpreter
{
    public PaymentResult InterpretResponse(IProviderCallResult callResult, PaymentRequest request)
    {
        using var scope = logger.BeginScope(
            new Dictionary<string, string>
            {
                ["MerchantId"] = request.MerchantId,
                ["OrderId"] = request.OrderId
            });

        // Provider threw an exception (network timeout, DNS failure, etc.) and HttpStatusCode is not OK
        if (callResult.Exception is not null &&
            (HttpStatusCode?)callResult.HttpStatusCode != HttpStatusCode.OK)
        {
                logger.LogError(callResult.Exception,
                    "Provider call failed with exception: {Message}",
                    callResult.Exception.Message);

                return new PaymentResult(
                    PaymentStatus.NetworkError,
                    ErrorMessage: $"Provider call failed: {callResult.Exception.Message}");
        }

        var httpStatus = callResult.HttpStatusCode;

        switch ((HttpStatusCode?)httpStatus)
        {
            // 200 OK — Everything worked as expected
            case HttpStatusCode.OK when callResult.Response is not null:
            {
                var response = callResult.Response;
                
                if (callResult.Exception is not null)
                {
                    logger.LogError(callResult.Exception,
                        "Provider return an exception, but  HttpStatusCode is 200 OK: {Message}",
                        callResult.Exception.Message);
                }
                
                return response.Status switch
                {
                    StatusConstants.Approved => HandleApproved(response, request),
                    StatusConstants.ThreedsRequired => HandleThreeDsRequired(response),
                    _ => HandleUnknownStatusOnHttpOk(response)
                };
            }

            // 422 Unprocessable Entity — payment declined by provider
            case HttpStatusCode.UnprocessableContent
                when callResult.Response is { Status: StatusConstants.Error } response:
            {
                logger.LogInformation(
                    "Payment declined. Reason={Reason}",
                    response.Reason);

                return new PaymentResult(PaymentStatus.Declined, DeclineReason: response.Reason);
            }

            // 400 Bad Request — often due to missing a required parameter
            case HttpStatusCode.BadRequest:
            {
                logger.LogError(
                    "Provider returned 400 Bad Request — likely missing or invalid parameter. Request={Request}",
                    JsonSerializer.Serialize(request));

                return new PaymentResult(
                    PaymentStatus.ProviderError,
                    ErrorMessage: "Bad request — often due to a missing or invalid parameter.");
            }

            // 401 Unauthorized — no valid API key provided
            case HttpStatusCode.Unauthorized:
            {
                logger.LogError("Provider returned 401 Unauthorized — no valid API key provided.");

                return new PaymentResult(
                    PaymentStatus.ProviderError,
                    ErrorMessage: "Unauthorized — no valid API key provided.");
            }

            // 402 Request Failed — parameters were valid but the request failed
            case HttpStatusCode.PaymentRequired:
            {
                logger.LogError("Provider returned 402 Request Failed — parameters valid but request failed.");

                return new PaymentResult(
                    PaymentStatus.ProviderError,
                    ErrorMessage: "Request failed — parameters were valid but the request failed.");
            }

            // 403 Forbidden — API key doesn't have permissions to perform the request
            case HttpStatusCode.Forbidden:
            {
                logger.LogError("Provider returned 403 Forbidden — API key lacks required permissions.");

                return new PaymentResult(
                    PaymentStatus.ProviderError,
                    ErrorMessage: "Forbidden — API key doesn't have permissions to perform the request.");
            }

            // 404 Not Found — requested resource doesn't exist, possible URL misconfiguration
            case HttpStatusCode.NotFound:
            {
                logger.LogError("Provider returned 404 Not Found — requested resource doesn't exist. Possible URL misconfiguration.");

                return new PaymentResult(
                    PaymentStatus.ProviderError,
                    ErrorMessage: "Not found — requested resource doesn't exist. Check provider URL configuration.");
            }

            // 409 Conflict — request conflicts with another request (e.g. duplicate idempotent key)
            case HttpStatusCode.Conflict:
            {
                logger.LogError("Provider returned 409 Conflict — request conflicts with another request, possibly a duplicate idempotent key.");

                return new PaymentResult(
                    PaymentStatus.ProviderError,
                    ErrorMessage: "Conflict — request conflicts with another request (possibly duplicate idempotent key).");
            }

            // 424 Failed Dependency — failure in an external dependency
            case HttpStatusCode.FailedDependency:
            {
                logger.LogError("Provider returned 424 Failed Dependency — external dependency failure.");

                return new PaymentResult(
                    PaymentStatus.ProviderError,
                    ErrorMessage: "External dependency failed.");
            }

            // 429 Too Many Requests — rate limited, recommend exponential backoff
            case HttpStatusCode.TooManyRequests:
            {
                logger.LogWarning("Provider returned 429 Too Many Requests — consider exponential backoff.");

                return new PaymentResult(
                    PaymentStatus.ProviderError,
                    ErrorMessage: "Too many requests — consider exponential backoff.");
            }

            // 500, 502, 503, 504 — Server Errors: something went wrong on the provider's end
            case HttpStatusCode.InternalServerError:
            case HttpStatusCode.BadGateway:
            case HttpStatusCode.ServiceUnavailable:
            case HttpStatusCode.GatewayTimeout:
            {
                logger.LogError("Provider server error HTTP {StatusCode} — something went wrong on the provider's end.", httpStatus);

                return new PaymentResult(
                    PaymentStatus.NetworkError,
                    ErrorMessage: $"Provider server error (HTTP {httpStatus}). These are typically transient — retry may succeed.");
            }

            // Unparseable response or unknown HTTP status
            default:
            {
                logger.LogError(
                    "Unexpected provider response. HTTP {StatusCode}, RawBody={RawBody}",
                    httpStatus, callResult.RawBody);

                return new PaymentResult(
                    PaymentStatus.ProviderError,
                    ErrorMessage: httpStatus.HasValue
                        ? $"Unexpected provider response: HTTP {httpStatus}"
                        : "Provider returned an unparseable response.");
            }
        }
    }

    private PaymentResult HandleApproved(ProviderResponse response, PaymentRequest request)
    {
        // Verify amount is present and positive
        if (response.Amount is not > 0)
        {
            logger.LogError(
                "Provider returned 'approved' without a valid amount. ProviderAmount={ProviderAmount}",
                response.Amount);

            return new PaymentResult(
                PaymentStatus.ProviderError,
                ErrorMessage: $"Provider returned approved without a valid amount (got {response.Amount?.ToString() ?? "null"}).");
        }

        // Verify amount matches
        if (response.Amount.Value != request.Amount)
        {
            logger.LogCritical(
                "AMOUNT MISMATCH: Requested={RequestedAmount}, Provider={ProviderAmount}",
                request.Amount, response.Amount.Value);

            return new PaymentResult(
                PaymentStatus.ProviderError,
                ErrorMessage: $"Amount mismatch: requested {request.Amount}, provider returned {response.Amount.Value}");
        }

        // Verify currency matches
        if (response.Currency is not null &&
            !string.Equals(response.Currency, request.Currency, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogCritical(
                "CURRENCY MISMATCH: Requested={RequestedCurrency}, Provider={ProviderCurrency}",
                request.Currency, response.Currency);

            return new PaymentResult(
                PaymentStatus.ProviderError,
                ErrorMessage: $"Currency mismatch: requested {request.Currency}, provider returned {response.Currency}");
        }

        logger.LogInformation(
            "Payment approved. ProviderRef={ProviderRef}",
            response.SystemOrderRef);

        return new PaymentResult(PaymentStatus.Approved, ProviderReference: response.SystemOrderRef);
    }

    private PaymentResult HandleThreeDsRequired(ProviderResponse response)
    {
        if (string.IsNullOrWhiteSpace(response.ThreeDsUrl))
        {
            logger.LogError("Provider returned 'threeds_required' but did not provide a redirect URL.");

            return new PaymentResult(
                PaymentStatus.ProviderError,
                ErrorMessage: "Provider indicated 3DS required but did not provide a URL.");
        }

        logger.LogInformation(
            "3DS authentication required. ProviderRef={ProviderRef}",
            response.SystemOrderRef);

        return new PaymentResult(PaymentStatus.ThreeDsRequired,
            ProviderReference: response.SystemOrderRef,
            ThreeDsUrl: response.ThreeDsUrl);
    }

    private PaymentResult HandleUnknownStatusOnHttpOk(ProviderResponse response)
    {
        logger.LogError(
            "Unknown provider status on HTTP 200. Status={ProviderStatus}",
            response.Status);

        return new PaymentResult(
            PaymentStatus.ProviderError,
            ErrorMessage: $"Unknown provider status: '{response.Status}'");
    }
}
