using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PaymentEngine.Models;
using PaymentEngine.Provider;

namespace PaymentEngine.Tests.Providers;

public class PaymentProviderClient(HttpClient httpClient, ILogger<PaymentProviderClient> logger) : IPaymentProviderClient
{
    public async Task<ProviderCallResult> ChargeAsync(ProviderRequest request, CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Sending charge request to provider: Amount={Amount}, Currency={Currency}",
                request.Amount, request.Currency);

            var response = await httpClient.PostAsJsonAsync("/charges", request, ct);
            var rawBody = await response.Content.ReadAsStringAsync(ct);
            var statusCode = (int)response.StatusCode;

            logger.LogInformation("Provider responded: HTTP {StatusCode}, Body length={Length}",
                statusCode, rawBody.Length);

            ProviderResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<ProviderResponse>(rawBody);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Failed to deserialize provider response. RawBody={RawBody}", rawBody);
                return new ProviderCallResult(statusCode, null, rawBody, null);
            }

            return new ProviderCallResult(statusCode, parsed, rawBody, null);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Provider request timed out");
            return new ProviderCallResult(null, null, null, ex);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex,
                "Network error calling provider. Verify that the provider base URL ({BaseUrl}) is reachable.",
                httpClient.BaseAddress);
            return new ProviderCallResult(null, null, null, ex);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex,
                "Invalid provider configuration. BaseAddress={BaseUrl}. " +
                "Ensure the provider URL is set correctly.",
                httpClient.BaseAddress);
            return new ProviderCallResult(null, null, null, ex);
        }
    }
}
