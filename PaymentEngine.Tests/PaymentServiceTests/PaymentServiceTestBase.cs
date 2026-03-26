using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PaymentEngine.Models;
using PaymentEngine.Provider;
using PaymentEngine.Services;
using PaymentEngine.Storage;
using PaymentEngine.Tests.Helpers;
using PaymentEngine.Tests.Providers;
using PaymentEngine.Validation;

namespace PaymentEngine.Tests.PaymentServiceTests;

public abstract class PaymentServiceTestBase
{
    protected readonly FakeHttpHandler Handler = new();
    private readonly PaymentStore _store = new();
    protected readonly PaymentService Service;

    protected static readonly PaymentRequest ValidRequest = new(
        MerchantId: "merchant_42",
        OrderId: "ord_abc123",
        Amount: 1500,
        Currency: "EUR",
        CardToken: "tok_visa_4242"
    );

    protected PaymentServiceTestBase()
    {
        var httpClient = new HttpClient(Handler) { BaseAddress = new Uri("https://api.paymentprovider.com") };
        var providerClient = new PaymentProviderClient(httpClient, NullLogger<PaymentProviderClient>.Instance);
        var validator = new PaymentRequestValidator();
        Service = new PaymentService(providerClient, _store, validator, NullLogger<PaymentService>.Instance);
    }

    protected void SetupProviderResponse(HttpStatusCode statusCode, ProviderResponse body)
    {
        var json = JsonSerializer.Serialize(body);
        Handler.SetupResponse(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });
    }
}