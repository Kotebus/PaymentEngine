using System.Net;
using PaymentEngine.Constants;
using PaymentEngine.Models;
using PaymentEngine.Provider;
using Xunit;

namespace PaymentEngine.Tests.PaymentServiceTests;

public class PaymentServiceUrlMisconfigurationTests : PaymentServiceTestBase
{
    [Fact]
    public async Task Provider_Http404_Returns_ProviderError()
    {
        Handler.SetupResponse(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("<html>Not Found</html>", System.Text.Encoding.UTF8, "text/html")
        });

        var result = await Service.CallAsync(ValidRequest, CancellationToken.None);

        Assert.Equal(PaymentStatus.ProviderError, result.Status);
        Assert.Contains("Not found", result.ErrorMessage!);
    }

    [Fact]
    public async Task Provider_Http502_Returns_NetworkError()
    {
        Handler.SetupResponse(new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("<html>Bad Gateway</html>", System.Text.Encoding.UTF8, "text/html")
        });

        var result = await Service.CallAsync(ValidRequest, CancellationToken.None);

        Assert.Equal(PaymentStatus.NetworkError, result.Status);
        Assert.Contains("502", result.ErrorMessage!);
    }

    [Fact]
    public async Task Infrastructure_Error_Not_Cached_Allows_Retry()
    {
        // First call — 503
        Handler.SetupResponse(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("Service Unavailable")
        });

        var first = await Service.CallAsync(ValidRequest, CancellationToken.None);
        Assert.Equal(PaymentStatus.NetworkError, first.Status);

        // Second call — provider recovered
        SetupProviderResponse(HttpStatusCode.OK, new ProviderResponse
        {
            Status = StatusConstants.Approved,
            SystemOrderRef = "ord_recovered",
            Amount = 1500,
            Currency = "EUR"
        });

        var second = await Service.CallAsync(ValidRequest, CancellationToken.None);
        Assert.Equal(PaymentStatus.Approved, second.Status);
    }

    [Fact]
    public async Task Connection_Refused_Returns_NetworkError()
    {
        Handler.Setup((_, _) => throw new HttpRequestException("Connection refused"));

        var result = await Service.CallAsync(ValidRequest, CancellationToken.None);

        Assert.Equal(PaymentStatus.NetworkError, result.Status);
        Assert.Contains("Connection refused", result.ErrorMessage!);
    }
}