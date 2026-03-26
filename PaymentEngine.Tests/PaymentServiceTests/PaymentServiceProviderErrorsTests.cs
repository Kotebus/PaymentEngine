using System.Net;
using PaymentEngine.Constants;
using PaymentEngine.Models;
using PaymentEngine.Provider;
using Xunit;

namespace PaymentEngine.Tests.PaymentServiceTests;

public class PaymentServiceProviderErrorsTests : PaymentServiceTestBase
{
    [Fact]
    public async Task Provider_Timeout_Returns_NetworkError()
    {
        const string exceptionMessage = "Request timed out";
        Handler.Setup((_, ct) => throw new TaskCanceledException(
            exceptionMessage,
            new TimeoutException(), ct));

        var result = await Service.CallAsync(ValidRequest, CancellationToken.None);

        Assert.Equal(PaymentStatus.NetworkError, result.Status);
        Assert.Contains(exceptionMessage, result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Provider_Http500_Returns_NetworkError()
    {
        const HttpStatusCode code = HttpStatusCode.InternalServerError;
        SetupProviderResponse(code, new ProviderResponse { Status = StatusConstants.Error, Reason = "internal" });

        var result = await Service.CallAsync(ValidRequest, CancellationToken.None);

        Assert.Equal(PaymentStatus.NetworkError, result.Status);
        Assert.Contains(((int)code).ToString(), result.ErrorMessage!);
    }

    [Fact]
    public async Task Unknown_Status_In_200_Returns_ProviderError()
    {
        const string status = "pending";
        SetupProviderResponse(HttpStatusCode.OK, new ProviderResponse
        {
            Status = status,
            SystemOrderRef = "ord_xxx",
            Amount = ValidRequest.Amount,
            Currency = ValidRequest.Currency
        });

        var result = await Service.CallAsync(ValidRequest, CancellationToken.None);

        Assert.Equal(PaymentStatus.ProviderError, result.Status);
        Assert.Contains(status, result.ErrorMessage!);
    }

    [Fact]
    public async Task Amount_Mismatch_Returns_ProviderError()
    {
        SetupProviderResponse(HttpStatusCode.OK, new ProviderResponse
        {
            Status = StatusConstants.Approved,
            SystemOrderRef = "ord_wrong_amount",
            Amount = ValidRequest.Amount + 100,
            Currency = ValidRequest.Currency
        });

        var result = await Service.CallAsync(ValidRequest, CancellationToken.None);

        Assert.Equal(PaymentStatus.ProviderError, result.Status);
        Assert.Contains("mismatch", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Currency_Mismatch_Returns_ProviderError()
    {
        SetupProviderResponse(HttpStatusCode.OK, new ProviderResponse
        {
            Status = StatusConstants.Approved,
            SystemOrderRef = "ord_wrong_ccy",
            Amount = ValidRequest.Amount,
            Currency = "USD"
        });

        //with statement just for readability
        var result = await Service.CallAsync(ValidRequest with { Currency = "EUR" }, CancellationToken.None);

        Assert.Equal(PaymentStatus.ProviderError, result.Status);
        Assert.Contains("mismatch", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unparseable_Body_Returns_ProviderError()
    {
        Handler.SetupResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("this is not json", System.Text.Encoding.UTF8, "text/plain")
        });

        var result = await Service.CallAsync(ValidRequest, CancellationToken.None);

        Assert.Equal(PaymentStatus.ProviderError, result.Status);
    }

    [Fact]
    public async Task Empty_Body_Returns_ProviderError()
    {
        Handler.SetupResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("", System.Text.Encoding.UTF8, "application/json")
        });

        var result = await Service.CallAsync(ValidRequest, CancellationToken.None);

        Assert.Equal(PaymentStatus.ProviderError, result.Status);
    }

    [Fact]
    public async Task Approved_Without_Amount_Returns_ProviderError()
    {
        SetupProviderResponse(HttpStatusCode.OK, new ProviderResponse { Status = StatusConstants.Approved });

        var result = await Service.CallAsync(ValidRequest, CancellationToken.None);

        Assert.Equal(PaymentStatus.ProviderError, result.Status);
        Assert.Contains("null", result.ErrorMessage!);
    }

    [Fact]
    public async Task Approved_With_Zero_Amount_Returns_ProviderError()
    {
        SetupProviderResponse(HttpStatusCode.OK, new ProviderResponse
        {
            Status = StatusConstants.Approved,
            SystemOrderRef = "ord_zero",
            Amount = 0,
            Currency = ValidRequest.Currency
        });

        var result = await Service.CallAsync(ValidRequest, CancellationToken.None);

        Assert.Equal(PaymentStatus.ProviderError, result.Status);
        Assert.Contains("0", result.ErrorMessage!);
    }

    [Fact]
    public async Task ThreeDsRequired_Without_Url_Returns_ProviderError()
    {
        SetupProviderResponse(HttpStatusCode.OK, new ProviderResponse
        {
            Status = StatusConstants.ThreedsRequired,
            SystemOrderRef = "ord_no_url",
            Amount = ValidRequest.Amount,
            Currency = ValidRequest.Currency
        });

        var result = await Service.CallAsync(ValidRequest, CancellationToken.None);

        Assert.Equal(PaymentStatus.ProviderError, result.Status);
        Assert.Contains("URL", result.ErrorMessage!);
    }
}