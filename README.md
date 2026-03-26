# Payment Engine

A payment processing service [PaymentService](Services/PaymentService.cs) that validates merchant requests, calls an 
external payment provider, and handles responses defensively.

## Prerequisites

[.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) or newer

## How to Run

1. Clone the repository
2. Open the solution in [Visual Studio](https://visualstudio.microsoft.com/), [VS Code](https://code.visualstudio.com/) (supports MacOS), 
[Rider](https://www.jetbrains.com/rider/) (supports MacOS), or any other compatible IDE
3. Run the project with
```bash
dotnet build
dotnet test PaymentEngine.Tests
```

## Architecture

Interface-driven design — all major components have interfaces for testability and DI:

| Layer                | Interface | Implementation                                |
|----------------------|---|-----------------------------------------------|
| Service              | `IPaymentService` | `PaymentService`                              |
| Validation           | `IPaymentRequestValidator` | `PaymentRequestValidator`                     |
| Provider             | `IPaymentProviderClient` | `PaymentProviderClient` (in [test project](PaymentEngine.Tests/Providers/PaymentProviderClient.cs)) |
| Response Interpreter | `IResponseInterpreter` (optional) | `DefaultResponseInterpreter`                  |
| Storage              | `IPaymentStore` | `PaymentStore`                                |

`IResponseInterpreter` is an optional dependency of `PaymentService` — when not provided,
`DefaultResponseInterpreter` is used by default.

`PaymentProviderClient` lives in [PaymentEngine.Tests/Providers/](PaymentEngine.Tests/Providers/PaymentProviderClient.cs) — the main project exposes only the 
`IPaymentProviderClient` interface, leaving the concrete HTTP implementation to the consumer.

## Key Decisions

### Idempotency

Since the idempotency key is not passed into the service’s main method, I construct it as a composite key using
`(MerchantId, OrderId)`. In the future, it would be better to delegate this responsibility to the consumer and pass 
the key explicitly as part of the input.

So requests are keyed by `(MerchantId, OrderId)`. Terminal outcomes (approved, declined, 3DS required) are cached in-memory
— a duplicate request returns the cached result without calling the provider again. Transient failures (network errors, 
provider errors) are **not** cached, so the caller can retry with the same `(MerchantId, OrderId)`.

A per-order `ConcurrentDictionary<string, Lazy<Task<IPaymentResult>>>` prevents two concurrent requests for the same 
order from both reaching the provider (double-charge protection), it's covered in 
[PaymentServiceConcurrencyTests](PaymentEngine.Tests/PaymentServiceTests/PaymentServiceConcurrencyTests.cs) in 
[PaymentServiceTests](PaymentEngine.Tests/PaymentServiceTests).

### Provider Trust Model

The provider is treated as untrusted:

- **All response fields are nullable** — we don't assume the provider returns everything documented.
- **Amount and currency are verified** against our request. A mismatch logs at `Critical` level and returns `ProviderError`.
- **Unknown HTTP statuses and unknown `status` values** are treated as `ProviderError`.
- **Unparseable responses** (non-JSON, empty body) are caught and the raw body is logged for debugging.
- **3DS without a URL** is treated as a provider error, not silently accepted.

### URL Misconfiguration Detection and Error Handling

[DefaultResponseInterpreter](Provider/DefaultResponseInterpreter.cs) explicitly checks for status codes from the provider and returns 
`NetworkError` with a diagnostic message about possible URL misconfiguration even if provider didn't return an exception 
message. These infrastructure errors are distinguished from business-level `ProviderError` and are not cached, allowing 
retries.

Based on [Stripe API errors doc](https://docs.stripe.com/api/errors.md).

### Logging

Structured logging with `ILogger` throughout. Key events logged:
- Every provider call (request and response)
- Validation failures
- Idempotent cache hits
- Amount/currency mismatches (Critical)
- Network errors, timeouts, and infrastructure failures
- Unknown provider responses (with raw body)
- Provider exceptions

### Testing Approach

Tests use a custom [FakeHttpHandler](PaymentEngine.Tests/Helpers/FakeHttpHandler.cs) that replaces `HttpClient`'s 
transport layer. No mocking frameworks — the handler is simple and gives full control over HTTP responses, timeouts, 
and network errors.

Tests covering: happy paths, all error scenarios, idempotency behavior, concurrency safety, URL misconfiguration 
detection, and input validation.

## What I'd Do Differently in Production

- **Persistent storage** — Replace `ConcurrentDictionary` with a database. Use a unique constraint on 
`(merchant_id, order_id)` and a state machine (pending → approved/declined) to handle the case where the process 
crashes between calling the provider and storing the result.
- **Reconciliation job** — Support pending status and periodically query the provider for transactions in "pending" 
state to resolve orphaned payments after network failures.
- **Retry with backoff** — For transient provider errors (5xx, timeouts), retry 2-3 times with exponential backoff 
before returning `NetworkError`.
- **Circuit breaker** — Stop calling the provider entirely if it's consistently failing, to avoid cascading failures.
- **Metrics and alerting** — Track success/decline/error rates, latency percentiles, and alert on anomalies (sudden 
spike in declines, amount mismatches).
- **Request/response audit log** — Store full request and response payloads (with PCI-compliant redaction) for dispute 
resolution and debugging.
- **Logging optimization** — see [Use the LoggerMessage delegates](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1848)

## Intentionally Skipped

- HTTP API layer (per task requirements)
- Real database
- Retry logic and circuit breaker
- Card token format validation
- Currency whitelist / amount upper bounds
- Configuration (bearer token, base URL, timeout are hardcoded or injected via DI)
