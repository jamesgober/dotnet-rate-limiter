# dotnet-rate-limiter

[![NuGet](https://img.shields.io/nuget/v/JG.RateLimiter?logo=nuget)](https://www.nuget.org/packages/JG.RateLimiter)
[![Downloads](https://img.shields.io/nuget/dt/JG.RateLimiter?color=%230099ff&logo=nuget)](https://www.nuget.org/packages/JG.RateLimiter)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](./LICENSE)
[![CI](https://github.com/jamesgober/dotnet-rate-limiter/actions/workflows/ci.yml/badge.svg)](https://github.com/jamesgober/dotnet-rate-limiter/actions)

---

High-performance rate limiting for .NET APIs. Supports token bucket, sliding window, fixed window, and concurrency limiting — with per-client and per-endpoint policies, distributed state via Redis, and ASP.NET Core middleware integration.


## Features

- **Token Bucket** — Smooth rate limiting with configurable burst capacity and refill rate
- **Sliding Window** — Accurate request counting over rolling time windows
- **Fixed Window** — Simple time-sliced rate limiting with automatic reset
- **Concurrency Limiter** — Cap simultaneous in-flight requests per client or endpoint
- **Per-Client Policies** — Rate limit by IP, API key, user ID, or custom partition key
- **Per-Endpoint Policies** — Different limits for different routes via attributes or conventions
- **Distributed State** — Optional Redis backend for rate limiting across multiple instances
- **Retry-After Headers** — Automatic `Retry-After` and `X-RateLimit-*` response headers
- **Middleware** — Drop-in ASP.NET Core middleware with `services.AddRateLimiting()`

## Installation

```bash
dotnet add package JG.RateLimiter
```

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

app.UseRateLimiting();
```

## Documentation

- **[API Reference](./docs/API.md)** — Full API documentation and examples

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

Licensed under the Apache License 2.0. See [LICENSE](./LICENSE) for details.

---

**Ready to get started?** Install via NuGet and check out the [API reference](./docs/API.md).
