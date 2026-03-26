using PaymentEngine.Models;
using PaymentEngine.Validation;
using Xunit;

namespace PaymentEngine.Tests;

public class PaymentRequestValidatorTests
{
    private static readonly PaymentRequest ValidRequest = new(
        MerchantId: "merchant_42",
        OrderId: "ord_abc123",
        Amount: 1500,
        Currency: "EUR",
        CardToken: "tok_visa_4242"
    );

    [Fact]
    public void Valid_Request_Has_No_Errors()
    {
        var errors = new PaymentRequestValidator().Validate(ValidRequest);
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Empty_MerchantId_Fails(string? merchantId)
    {
        var request = ValidRequest with { MerchantId = merchantId! };
        var errors = new PaymentRequestValidator().Validate(request);
        Assert.Contains(errors, e => e.Contains("MerchantId"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Empty_OrderId_Fails(string? orderId)
    {
        var request = ValidRequest with { OrderId = orderId! };
        var errors = new PaymentRequestValidator().Validate(request);
        Assert.Contains(errors, e => e.Contains("OrderId"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Non_Positive_Amount_Fails(long amount)
    {
        var request = ValidRequest with { Amount = amount };
        var errors = new PaymentRequestValidator().Validate(request);
        Assert.Contains(errors, e => e.Contains("Amount"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("eu")]
    [InlineData("euro")]
    [InlineData("123")]
    public void Invalid_Currency_Fails(string? currency)
    {
        var request = ValidRequest with { Currency = currency! };
        var errors = new PaymentRequestValidator().Validate(request);
        Assert.Contains(errors, e => e.Contains("Currency", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Empty_CardToken_Fails(string? cardToken)
    {
        var request = ValidRequest with { CardToken = cardToken! };
        var errors = new PaymentRequestValidator().Validate(request);
        Assert.Contains(errors, e => e.Contains("CardToken"));
    }

    [Fact]
    public void Multiple_Errors_Returned_Together()
    {
        var request = new PaymentRequest("", "", 0, "", "");
        var errors = new PaymentRequestValidator().Validate(request);
        Assert.True(errors.Count >= 4);
    }
}
