namespace PaymentEngine.Tests.Helpers;

public class FakeHttpHandler : HttpMessageHandler
{
    private Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler
        = (_, _) => throw new InvalidOperationException("FakeHttpHandler not configured.");

    private int _callCount;
    public int CallCount => _callCount;

    public void Setup(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) => 
        _handler = handler;

    public void SetupResponse(HttpResponseMessage response) => 
        _handler = (_, _) => Task.FromResult(response);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);
        return await _handler(request, cancellationToken);
    }
}
