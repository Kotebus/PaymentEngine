using System.Net;
using System.Text.Json;
using PaymentEngine.Constants;
using PaymentEngine.Models;
using PaymentEngine.Provider;
using Xunit;

namespace PaymentEngine.Tests.PaymentServiceTests;

public class PaymentServiceConcurrencyTests : PaymentServiceTestBase
{
    [Fact]
    public async Task CallAsync_Should_Not_Lose_Calls_Under_Concurrency()
    {
        const int concurrency = 10;

        var barrier = new Barrier(concurrency);
        
        SetupProviderResponse(HttpStatusCode.OK, new ProviderResponse
        {
            Status = StatusConstants.Approved,
            SystemOrderRef = "ord_abc123",
            Amount = ValidRequest.Amount,
            Currency = ValidRequest.Currency
        });

        var tasks = Enumerable.Range(0, concurrency).Select( _ => Task.Run(async () =>
        {
            barrier.SignalAndWait();
            return await Service.CallAsync(ValidRequest, CancellationToken.None);
        }))
        .ToList();

        var results = await Task.WhenAll(tasks);
        
        Assert.All(results, r => Assert.Equal(PaymentStatus.Approved, r.Status));
        Assert.Equal(1, Handler.CallCount);
    }
    
    [Fact]
    public async Task Concurrent_Calls_Same_Order_Only_One_Provider_Call()
    {
        var callDelay = new TaskCompletionSource<bool>();

        Handler.Setup(async (_, _) =>
        {
            await callDelay.Task;
            var json = JsonSerializer.Serialize(new ProviderResponse
            {
                Status = StatusConstants.Approved,
                SystemOrderRef = "ord_concurrent",
                Amount = ValidRequest.Amount,
                Currency = ValidRequest.Currency
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var task1 = Service.CallAsync(ValidRequest, CancellationToken.None);
        var task2 = Service.CallAsync(ValidRequest, CancellationToken.None);

        // Release the provider call
        callDelay.SetResult(true);

        var results = await Task.WhenAll(task1, task2);

        Assert.All(results, r => Assert.Equal(PaymentStatus.Approved, r.Status));
        Assert.Equal(1, Handler.CallCount);
    }
}