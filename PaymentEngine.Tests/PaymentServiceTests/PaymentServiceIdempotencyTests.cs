using System.Net;
using PaymentEngine.Constants;
using PaymentEngine.Models;
using PaymentEngine.Provider;
using Xunit;

namespace PaymentEngine.Tests.PaymentServiceTests;

public class PaymentServiceIdempotencyTests : PaymentServiceTestBase
{
    [Fact]
    public async Task Duplicate_Request_Returns_Cached_Result_Without_Calling_Provider()
    {
        SetupProviderResponse(HttpStatusCode.OK, new ProviderResponse
        {
            Status = StatusConstants.Approved,
            SystemOrderRef = "ord_abc123",
            Amount = ValidRequest.Amount,
            Currency = ValidRequest.Currency
        });

        var first = await Service.CallAsync(ValidRequest, CancellationToken.None);
        var second = await Service.CallAsync(ValidRequest, CancellationToken.None);

        Assert.Equal(PaymentStatus.Approved, first.Status);
        Assert.Equal(PaymentStatus.Approved, second.Status);
        Assert.Equal(first, second);
        Assert.Equal(1, Handler.CallCount);
    }

    [Fact]
    public async Task Declined_Result_Is_Cached_And_Retry_Blocked()
    {
        SetupProviderResponse(HttpStatusCode.UnprocessableEntity, new ProviderResponse
        {
            Status = StatusConstants.Error,
            Reason = "card_declined"
        });

        var first = await Service.CallAsync(ValidRequest, CancellationToken.None);
        var second = await Service.CallAsync(ValidRequest, CancellationToken.None);

        Assert.Equal(PaymentStatus.Declined, first.Status);
        Assert.Equal(PaymentStatus.Declined, second.Status);
        Assert.Equal(1, Handler.CallCount);
    }

    [Fact]
    public async Task Network_Error_Not_Cached_Allows_Retry()
    {
        const string orderRef = "ord_abc123";
        // First call — network error
        Handler.Setup((_, _) => throw new HttpRequestException("Connection refused"));

        var first = await Service.CallAsync(ValidRequest, CancellationToken.None);
        Assert.Equal(PaymentStatus.NetworkError, first.Status);

        // Second call — provider is back, returns approved
        SetupProviderResponse(HttpStatusCode.OK, new ProviderResponse
        {
            Status = StatusConstants.Approved,
            SystemOrderRef = orderRef,
            Amount = ValidRequest.Amount,
            Currency = ValidRequest.Currency
        });

        var second = await Service.CallAsync(ValidRequest, CancellationToken.None);
        Assert.Equal(PaymentStatus.Approved, second.Status);
        Assert.Equal(orderRef, second.ProviderReference);
    }
}