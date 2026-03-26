using Microsoft.Extensions.Logging.Abstractions;
using PaymentEngine.Constants;
using PaymentEngine.Models;
using PaymentEngine.Provider;
using PaymentEngine.Services;
using Xunit;

namespace PaymentEngine.Tests;

public class DefaultResponseInterpreterTests
{
    private readonly DefaultResponseInterpreter _interpreter = new(NullLogger<IPaymentService>.Instance);

    private static readonly PaymentRequest DefaultRequest = new(
        MerchantId: "merchant_1",
        OrderId: "ord_1",
        Amount: 1000,
        Currency: "EUR",
        CardToken: "tok_1"
    );

    private static PaymentResult Interpret(
        DefaultResponseInterpreter interpreter,
        int? httpStatusCode,
        ProviderResponse? response = null,
        string? rawBody = null,
        Exception? exception = null,
        PaymentRequest? request = null)
    {
        var callResult = new ProviderCallResult(httpStatusCode, response, rawBody, exception);
        return interpreter.InterpretResponse(callResult, request ?? DefaultRequest);
    }

    private PaymentResult Interpret(
        int? httpStatusCode,
        ProviderResponse? response = null,
        string? rawBody = null,
        Exception? exception = null,
        PaymentRequest? request = null)
        => Interpret(_interpreter, httpStatusCode, response, rawBody, exception, request);

    #region HTTP 200 — Approved

    [Fact]
    public void Approved_WithValidAmountAndCurrency_ReturnsApproved()
    {
        var result = Interpret(200, new ProviderResponse
        {
            Status = StatusConstants.Approved,
            SystemOrderRef = "ref_123",
            Amount = 1000,
            Currency = "EUR"
        });

        Assert.Equal(PaymentStatus.Approved, result.Status);
        Assert.Equal("ref_123", result.ProviderReference);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Approved_WithNullAmount_ReturnsProviderError()
    {
        var result = Interpret(200, new ProviderResponse
        {
            Status = StatusConstants.Approved,
            Amount = null,
            Currency = "EUR"
        });

        Assert.Equal(PaymentStatus.ProviderError, result.Status);
        Assert.Contains("null", result.ErrorMessage!);
    }

    [Fact]
    public void Approved_WithZeroAmount_ReturnsProviderError()
    {
        var result = Interpret(200, new ProviderResponse
        {
            Status = StatusConstants.Approved,
            Amount = 0,
            Currency = "EUR"
        });

        Assert.Equal(PaymentStatus.ProviderError, result.Status);
    }

    [Fact]
    public void Approved_WithMismatchedAmount_ReturnsProviderError()
    {
        var result = Interpret(200, new ProviderResponse
        {
            Status = StatusConstants.Approved,
            Amount = 9999,
            Currency = "EUR"
        });

        Assert.Equal(PaymentStatus.ProviderError, result.Status);
        Assert.Contains("mismatch", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Approved_WithMismatchedCurrency_ReturnsProviderError()
    {
        var result = Interpret(200, new ProviderResponse
        {
            Status = StatusConstants.Approved,
            Amount = 1000,
            Currency = "USD"
        });

        Assert.Equal(PaymentStatus.ProviderError, result.Status);
        Assert.Contains("mismatch", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Approved_WithNullProviderCurrency_ReturnsApproved()
    {
        var result = Interpret(200, new ProviderResponse
        {
            Status = StatusConstants.Approved,
            SystemOrderRef = "ref_456",
            Amount = 1000,
            Currency = null
        });

        Assert.Equal(PaymentStatus.Approved, result.Status);
        Assert.Equal("ref_456", result.ProviderReference);
    }

    #endregion

    #region HTTP 200 — 3DS Required

    [Fact]
    public void ThreeDsRequired_WithUrl_ReturnsThreeDsRequired()
    {
        var result = Interpret(200, new ProviderResponse
        {
            Status = StatusConstants.ThreedsRequired,
            SystemOrderRef = "ref_3ds",
            ThreeDsUrl = "https://bank.example/3ds/challenge"
        });

        Assert.Equal(PaymentStatus.ThreeDsRequired, result.Status);
        Assert.Equal("https://bank.example/3ds/challenge", result.ThreeDsUrl);
        Assert.Equal("ref_3ds", result.ProviderReference);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ThreeDsRequired_WithoutUrl_ReturnsProviderError(string? url)
    {
        var result = Interpret(200, new ProviderResponse
        {
            Status = StatusConstants.ThreedsRequired,
            ThreeDsUrl = url
        });

        Assert.Equal(PaymentStatus.ProviderError, result.Status);
        Assert.Contains("3DS", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region HTTP 200 — Unknown status

    [Fact]
    public void UnknownStatus_OnHttp200_ReturnsProviderError()
    {
        var result = Interpret(200, new ProviderResponse
        {
            Status = "pending",
            Amount = 1000,
            Currency = "EUR"
        });

        Assert.Equal(PaymentStatus.ProviderError, result.Status);
        Assert.Contains("pending", result.ErrorMessage!);
    }

    #endregion

    #region HTTP 200 — with Exception

    [Fact]
    public void Http200_WithException_StillProcessesResponse()
    {
        var result = Interpret(200,
            response: new ProviderResponse
            {
                Status = StatusConstants.Approved,
                SystemOrderRef = "ref_ex",
                Amount = 1000,
                Currency = "EUR"
            },
            exception: new Exception("partial failure"));

        Assert.Equal(PaymentStatus.Approved, result.Status);
        Assert.Equal("ref_ex", result.ProviderReference);
    }

    #endregion

    #region HTTP 200 — null Response

    [Fact]
    public void Http200_WithNullResponse_ReturnsProviderError()
    {
        var result = Interpret(200, response: null);

        Assert.Equal(PaymentStatus.ProviderError, result.Status);
    }

    #endregion

    #region HTTP 422 — Declined

    [Fact]
    public void Http422_WithErrorStatus_ReturnsDeclined()
    {
        var result = Interpret(422, new ProviderResponse
        {
            Status = StatusConstants.Error,
            Reason = "insufficient_funds"
        });

        Assert.Equal(PaymentStatus.Declined, result.Status);
        Assert.Equal("insufficient_funds", result.DeclineReason);
    }

    [Fact]
    public void Http422_WithNonErrorStatus_FallsToDefault()
    {
        var result = Interpret(422, new ProviderResponse
        {
            Status = "something_else"
        });

        Assert.Equal(PaymentStatus.ProviderError, result.Status);
    }

    #endregion

    #region HTTP 4xx errors

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(402)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(424)]
    [InlineData(429)]
    public void Http4xx_ReturnsProviderError(int statusCode)
    {
        var result = Interpret(statusCode);

        Assert.Equal(PaymentStatus.ProviderError, result.Status);
        Assert.NotNull(result.ErrorMessage);
    }

    #endregion

    #region HTTP 5xx — Server errors

    [Theory]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public void Http5xx_ReturnsNetworkError(int statusCode)
    {
        var result = Interpret(statusCode);

        Assert.Equal(PaymentStatus.NetworkError, result.Status);
        Assert.Contains(statusCode.ToString(), result.ErrorMessage!);
    }

    #endregion

    #region Exception without HTTP 200

    [Fact]
    public void Exception_WithNon200Status_ReturnsNetworkError()
    {
        var result = Interpret(500, exception: new TimeoutException("timed out"));

        Assert.Equal(PaymentStatus.NetworkError, result.Status);
        Assert.Contains("timed out", result.ErrorMessage!);
    }

    [Fact]
    public void Exception_WithNullStatusCode_ReturnsNetworkError()
    {
        var result = Interpret(null, exception: new Exception("connection refused"));

        Assert.Equal(PaymentStatus.NetworkError, result.Status);
        Assert.Contains("connection refused", result.ErrorMessage!);
    }

    #endregion

    #region Default — unknown HTTP status

    [Fact]
    public void UnknownHttpStatus_ReturnsProviderError()
    {
        var result = Interpret(418);

        Assert.Equal(PaymentStatus.ProviderError, result.Status);
        Assert.Contains("418", result.ErrorMessage!);
    }

    [Fact]
    public void NullHttpStatus_NoException_ReturnsUnparseableError()
    {
        var result = Interpret(null);

        Assert.Equal(PaymentStatus.ProviderError, result.Status);
        Assert.Contains("unparseable", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
