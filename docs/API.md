# API Reference

## Installation

```bash
dotnet add package JG.RateLimiter
```

Add the namespace:

```csharp
using JG.RateLimiter;
using JG.RateLimiter.Policies;
```

---

## Quick Start

```csharp
builder.Services.AddRateLimiting(options =>
{
    options.AddPolicy("api", new TokenBucketPolicy
    {
        PermitLimit = 100,
        Window = TimeSpan.FromMinutes(1),
        BurstLimit = 20,
        PartitionBy = context => context.Connection.RemoteIpAddress?.ToString()
    });
});

app.UseRouting();
app.UseRateLimiting();
app.MapControllers();
```

---

## Middleware

### `AddRateLimiting(Action<RateLimitingOptions>)`

Registers rate limiting services with the DI container.

```csharp
builder.Services.AddRateLimiting(options =>
{
    options.DefaultPolicyName = "global";
    options.RejectionStatusCode = 429;

    options.AddPolicy("global", new FixedWindowPolicy
    {
        PermitLimit = 1000,
        Window = TimeSpan.FromHours(1)
    });

    options.AddPolicy("strict", new TokenBucketPolicy
    {
        PermitLimit = 10,
        Window = TimeSpan.FromMinutes(1),
        BurstLimit = 5,
        PartitionBy = ctx => ctx.Request.Headers["X-Api-Key"].ToString()
    });
});
```

### `UseRateLimiting()`

Adds the rate limiting middleware to the pipeline. Place after `UseRouting()` so
endpoint metadata is available.

```csharp
app.UseRouting();
app.UseRateLimiting();
```

### `[RateLimit("policyName")]`

Apply to a controller or action to select a specific policy. Overrides the default policy.

```csharp
[RateLimit("strict")]
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    [HttpPost]
    public IActionResult Create() => Ok();
}
```

### `[DisableRateLimiting]`

Opt out an endpoint from all rate limiting, including the default policy.

```csharp
[DisableRateLimiting]
[HttpGet("health")]
public IActionResult HealthCheck() => Ok("healthy");
```

---

## RateLimitingOptions

| Property | Type | Default | Description |
|---|---|---|---|
| `DefaultPolicyName` | `string?` | `null` | Policy applied to endpoints without `[RateLimit]`. |
| `RejectionStatusCode` | `int` | `429` | HTTP status code for rejected requests. |
| `OnRejected` | `Func<HttpContext, RateLimitLease, CancellationToken, ValueTask>?` | `null` | Custom rejection handler. When set, the middleware delegates response writing to this callback. |
| `IdleTimeout` | `TimeSpan` | `10 minutes` | How long an idle partition limiter lives before cleanup. |

### Methods

- **`AddPolicy(string name, IRateLimiterPolicy policy)`** — Registers a named policy. Returns the options instance for chaining.

---

## Policies

All policies default `PartitionBy` to the client's remote IP address. Set it to
customize partitioning.

### TokenBucketPolicy

Smooth rate limiting with burst support. Tokens replenish continuously.

| Property | Type | Description |
|---|---|---|
| `PermitLimit` | `int` | Tokens added per `Window`. |
| `Window` | `TimeSpan` | Replenishment period. |
| `BurstLimit` | `int` | Maximum token capacity (burst ceiling). |
| `PartitionBy` | `Func<HttpContext, string?>` | Partition key resolver. |

```csharp
new TokenBucketPolicy
{
    PermitLimit = 100,
    Window = TimeSpan.FromMinutes(1),
    BurstLimit = 20
}
```

### FixedWindowPolicy

Counts requests in non-overlapping time windows. Counter resets when the window expires.

| Property | Type | Description |
|---|---|---|
| `PermitLimit` | `int` | Max requests per window. |
| `Window` | `TimeSpan` | Window duration. |
| `PartitionBy` | `Func<HttpContext, string?>` | Partition key resolver. |

```csharp
new FixedWindowPolicy
{
    PermitLimit = 1000,
    Window = TimeSpan.FromHours(1)
}
```

### SlidingWindowPolicy

Counts requests over a rolling window divided into segments. Provides smoother
enforcement than fixed windows.

| Property | Type | Default | Description |
|---|---|---|---|
| `PermitLimit` | `int` | — | Max requests in the rolling window. |
| `Window` | `TimeSpan` | — | Total window duration. |
| `SegmentsPerWindow` | `int` | `3` | Number of sub-segments. Higher = finer granularity. |
| `PartitionBy` | `Func<HttpContext, string?>` | Remote IP | Partition key resolver. |

```csharp
new SlidingWindowPolicy
{
    PermitLimit = 100,
    Window = TimeSpan.FromMinutes(1),
    SegmentsPerWindow = 6
}
```

### ConcurrencyPolicy

Caps simultaneous in-flight requests. Permits are released when the lease is disposed.

| Property | Type | Description |
|---|---|---|
| `PermitLimit` | `int` | Max concurrent operations. |
| `PartitionBy` | `Func<HttpContext, string?>` | Partition key resolver. |

```csharp
new ConcurrencyPolicy
{
    PermitLimit = 10
}
```

---

## Algorithms (Standalone)

The rate limiter algorithms can be used directly without ASP.NET Core middleware.

### TokenBucketRateLimiter

```csharp
using var limiter = new TokenBucketRateLimiter(
    permitLimit: 100,
    window: TimeSpan.FromMinutes(1),
    burstLimit: 20);

using RateLimitLease lease = limiter.AttemptAcquire();
if (lease.IsAcquired)
{
    // proceed
}
```

### FixedWindowRateLimiter

```csharp
using var limiter = new FixedWindowRateLimiter(
    permitLimit: 1000,
    window: TimeSpan.FromHours(1));
```

### SlidingWindowRateLimiter

```csharp
using var limiter = new SlidingWindowRateLimiter(
    permitLimit: 100,
    window: TimeSpan.FromMinutes(1),
    segmentsPerWindow: 6);
```

### ConcurrencyLimiter

```csharp
using var limiter = new ConcurrencyLimiter(permitLimit: 10);

using RateLimitLease lease = limiter.AttemptAcquire();
if (lease.IsAcquired)
{
    // do work
}
// permit is returned when lease is disposed
```

---

## Abstractions

### IRateLimiter

Core interface implemented by all algorithms.

```csharp
public interface IRateLimiter : IAsyncDisposable, IDisposable
{
    RateLimitLease AttemptAcquire(int permitCount = 1);
    ValueTask<RateLimitLease> AcquireAsync(int permitCount = 1, CancellationToken cancellationToken = default);
    RateLimitStatistics GetStatistics();
}
```

### RateLimitLease

Returned by `AttemptAcquire` / `AcquireAsync`. Must be disposed (especially for
concurrency limiters).

| Property | Type | Description |
|---|---|---|
| `IsAcquired` | `bool` | Whether permits were granted. |
| `Limit` | `long` | Configured permit limit. |
| `Remaining` | `long` | Permits remaining after this acquisition. |
| `RetryAfter` | `TimeSpan?` | Suggested wait time (on rejection). |
| `ResetAfter` | `TimeSpan?` | Time until the current window resets. |

### RateLimitStatistics

```csharp
var stats = limiter.GetStatistics();
Console.WriteLine($"Available: {stats.CurrentAvailablePermits}");
Console.WriteLine($"Successful: {stats.TotalSuccessfulLeases}");
Console.WriteLine($"Rejected: {stats.TotalFailedLeases}");
```

### IRateLimiterPolicy

Implement this interface to create custom policies.

```csharp
public interface IRateLimiterPolicy
{
    Func<HttpContext, string?> PartitionBy { get; }
    IRateLimiter CreateLimiter(TimeProvider timeProvider);
}
```

### IRateLimitStore

Storage abstraction for limiter instances. The default `InMemoryRateLimitStore`
is registered automatically. Implement this interface for distributed scenarios
(e.g., Redis).

```csharp
public interface IRateLimitStore : IDisposable
{
    IRateLimiter GetOrCreate(string policyName, string partitionKey, Func<IRateLimiter> factory);
    bool TryRemove(string policyName, string partitionKey);
    void Clear();
}
```

To use a custom store, register it before calling `AddRateLimiting`:

```csharp
// Replace the built-in in-memory store with your own implementation.
builder.Services.AddSingleton<IRateLimitStore, MyRedisRateLimitStore>();
builder.Services.AddRateLimiting(options => { ... });
```

---

## Response Headers

The middleware adds these headers to every response passing through a rate-limited endpoint:

| Header | When | Example |
|---|---|---|
| `X-RateLimit-Limit` | Always | `100` |
| `X-RateLimit-Remaining` | Always | `42` |
| `X-RateLimit-Reset` | When available | `30` (seconds) |
| `Retry-After` | On 429 rejection | `5` (seconds) |

---

## Custom Rejection Handling

```csharp
builder.Services.AddRateLimiting(options =>
{
    options.AddPolicy("api", new FixedWindowPolicy
    {
        PermitLimit = 100,
        Window = TimeSpan.FromMinutes(1)
    });

    options.DefaultPolicyName = "api";

    options.OnRejected = async (context, lease, cancellationToken) =>
    {
        context.Response.StatusCode = 429;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            """{"error":"Rate limit exceeded","retryAfter":""" +
            (lease.RetryAfter?.TotalSeconds ?? 0) + "}",
            cancellationToken);
    };
});
```

---

## Testing

All algorithms accept an optional `TimeProvider` parameter for deterministic testing:

```csharp
var fakeTime = new FakeTimeProvider(); // your own TimeProvider subclass
using var limiter = new TokenBucketRateLimiter(
    permitLimit: 10,
    window: TimeSpan.FromSeconds(10),
    burstLimit: 5,
    timeProvider: fakeTime);

limiter.AttemptAcquire(); // uses fakeTime for token calculations
fakeTime.Advance(TimeSpan.FromSeconds(5));
limiter.AttemptAcquire(); // tokens have been replenished
```

Register a custom `TimeProvider` in DI to control time across the middleware:

```csharp
services.AddSingleton<TimeProvider>(myFakeTimeProvider);
services.AddRateLimiting(options => { ... });
```
