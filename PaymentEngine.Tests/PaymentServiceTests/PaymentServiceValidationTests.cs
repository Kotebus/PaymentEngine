using System.Net;
using PaymentEngine.Constants;
using PaymentEngine.Models;
using Xunit;

namespace PaymentEngine.Tests.PaymentServiceTests;

public class PaymentServiceValidationTests : PaymentServiceTestBase
{
    public static readonly TheoryData<PaymentRequest> InvalidRequests =
    [
        ValidRequest with { Amount = -1 },
        ValidRequest with { Amount = 0 },
        ValidRequest with { OrderId = "" },
        ValidRequest with { MerchantId = "" },
        ValidRequest with { Currency = "" },
        ValidRequest with { Currency = "X" },
        ValidRequest with { Currency = "euro" },
        ValidRequest with { CardToken = "" },
        new PaymentRequest("", "", 0, "X", "")
    ];
    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task Invalid_Request_Returns_ValidationError(PaymentRequest badRequest)
    {
        var result = await Service.CallAsync(badRequest, CancellationToken.None);

        Assert.Equal(PaymentStatus.ValidationError, result.Status);
        Assert.NotNull(result.ErrorMessage);
        Assert.Equal(0, Handler.CallCount);
    }

    [Fact]
    public async Task Valid_Request_Has_No_Errors()
    {
        SetupProviderResponse(HttpStatusCode.OK, new ProviderResponse
        {
            Status = StatusConstants.Approved,
            SystemOrderRef = "ord_98f7a6b5c4d3",
            Amount = ValidRequest.Amount,
            Currency = ValidRequest.Currency
        });
        
        var result = await Service.CallAsync(ValidRequest, CancellationToken.None);

        Assert.NotEqual(PaymentStatus.ValidationError, result.Status);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(1, Handler.CallCount);
    }
}