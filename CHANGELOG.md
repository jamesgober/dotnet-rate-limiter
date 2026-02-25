# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-02-24

### Added

- Token bucket rate limiter with configurable burst capacity and replenishment rate
- Fixed window rate limiter with automatic window reset
- Sliding window rate limiter with configurable segment granularity
- Concurrency limiter for capping simultaneous in-flight requests
- Per-client partitioning via PartitionBy delegates (IP, API key, custom)
- Per-endpoint policy selection via `[RateLimit]` attribute
- `[DisableRateLimiting]` attribute for opting endpoints out of rate limiting
- Default policy support for global rate limiting
- `RateLimitLease` with IsAcquired, Remaining, RetryAfter, and ResetAfter metadata
- `RateLimitStatistics` for runtime monitoring of limiter state
- ASP.NET Core middleware with `services.AddRateLimiting()` and `app.UseRateLimiting()`
- Automatic `Retry-After` and `X-RateLimit-*` response headers
- Configurable rejection status code and `OnRejected` callback
- In-memory rate limit store with automatic idle entry cleanup
- `IRateLimitStore` abstraction for pluggable distributed backends
- Custom `TimeProvider` injection for testable time-dependent logic
- Full XML documentation on all public APIs

### Security

- Integer overflow protection in permit count comparisons across all algorithms
- Graceful degradation when `PartitionBy` delegate throws (falls back to global partition)
- `OnRejected` callback exceptions are caught and logged instead of crashing the pipeline
- Empty/null partition keys fall back to a global partition rather than creating invalid store keys
- `HasStarted` guard prevents header writes after response has begun streaming

[1.0.0]: https://github.com/jamesgober/dotnet-rate-limiter/releases/tag/v1.0.0
