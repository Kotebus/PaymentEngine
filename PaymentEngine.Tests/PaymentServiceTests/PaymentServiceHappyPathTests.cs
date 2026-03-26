using System.Net;
using PaymentEngine.Constants;
using PaymentEngine.Models;
using Xunit;

namespace PaymentEngine.Tests.PaymentServiceTests;

public class PaymentServiceHappyPathTests : PaymentServiceTestBase
{
    [Fact]
    public async Task Approved_Payment_Returns_Success()
    {
        SetupProviderResponse(HttpStatusCode.OK, new ProviderResponse
        {
            Status = StatusConstants.Approved,
            SystemOrderRef = "ord_98f7a6b5c4d3",
            Amount = ValidRequest.Amount,
            Currency = ValidRequest.Currency,
        });

        var result = await Service.CallAsync(ValidRequest, CancellationToken.None);

        Assert.Equal(PaymentStatus.Approved, result.Status);
        Assert.Equal("ord_98f7a6b5c4d3", result.ProviderReference);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ThreeDsRequired_Returns_Url()
    {
        const string threedsUrl = "https://provider.example/3ds/abc123";
        const string orderRef = "ord_abc123";
        SetupProviderResponse(HttpStatusCode.OK, new ProviderResponse
        {
            Status = StatusConstants.ThreedsRequired,
            SystemOrderRef = orderRef,
            Amount = ValidRequest.Amount,
            Currency = ValidRequest.Currency,
            ThreeDsUrl = threedsUrl
        });

        var result = await Service.CallAsync(ValidRequest, CancellationToken.None);

        Assert.Equal(PaymentStatus.ThreeDsRequired, result.Status);
        Assert.Equal(threedsUrl, result.ThreeDsUrl);
        Assert.Equal(orderRef, result.ProviderReference);
    }

    [Fact]
    public async Task CardDeclined_Returns_Reason()
    {
        const string reason = "card_declined";
        SetupProviderResponse(HttpStatusCode.UnprocessableEntity, new ProviderResponse
        {
            Status = StatusConstants.Error,
            Reason = reason
        });

        var result = await Service.CallAsync(ValidRequest, CancellationToken.None);

        Assert.Equal(PaymentStatus.Declined, result.Status);
        Assert.Equal(reason, result.DeclineReason);
    }   
}