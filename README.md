<div align="center">
    <img width="120px" height="auto" src="https://raw.githubusercontent.com/jamesgober/jamesgober/main/media/icons/hexagon-3.svg" alt="Triple Hexagon">
    <h1>
        <strong>dotnet-rate-limiter</strong>
        <sup><br><sub>REQUEST RATE LIMITING</sub></sup>
    </h1>
    <div>
        <a href="https://www.nuget.org/packages/dotnet-rate-limiter"><img alt="NuGet" src="https://img.shields.io/nuget/v/dotnet-rate-limiter"></a>
        <span>&nbsp;</span>
        <a href="https://www.nuget.org/packages/dotnet-rate-limiter"><img alt="NuGet Downloads" src="https://img.shields.io/nuget/dt/dotnet-rate-limiter?color=%230099ff"></a>
        <span>&nbsp;</span>
        <a href="./LICENSE" title="License"><img alt="License" src="https://img.shields.io/badge/license-Apache--2.0-blue.svg"></a>
        <span>&nbsp;</span>
        <a href="https://github.com/jamesgober/dotnet-rate-limiter/actions"><img alt="GitHub CI" src="https://github.com/jamesgober/dotnet-rate-limiter/actions/workflows/ci.yml/badge.svg"></a>
    </div>
</div>
<br>
<p>
    High-performance rate limiting library for .NET APIs. Supports token bucket, sliding window, fixed window, and concurrency limiting strategies — with per-client and per-endpoint policies, distributed state via Redis, and ASP.NET Core middleware integration.
</p>

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

<br>

## Installation

```bash
dotnet add package dotnet-rate-limiter
```

<br>

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

<br>

## Documentation

- **[API Reference](./docs/API.md)** — Full API documentation and examples

<br>

## Contributing

Contributions welcome. Please:
1. Ensure all tests pass before submitting
2. Follow existing code style and patterns
3. Update documentation as needed

<br>

## Testing

```bash
dotnet test
```

<br>
<hr>
<br>

<div id="license">
    <h2>⚖️ License</h2>
    <p>Licensed under the <b>Apache License</b>, version 2.0 (the <b>"License"</b>); you may not use this software, including, but not limited to the source code, media files, ideas, techniques, or any other associated property or concept belonging to, associated with, or otherwise packaged with this software except in compliance with the <b>License</b>.</p>
    <p>You may obtain a copy of the <b>License</b> at: <a href="http://www.apache.org/licenses/LICENSE-2.0" title="Apache-2.0 License" target="_blank">http://www.apache.org/licenses/LICENSE-2.0</a>.</p>
    <p>Unless required by applicable law or agreed to in writing, software distributed under the <b>License</b> is distributed on an "<b>AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND</b>, either express or implied.</p>
    <p>See the <a href="./LICENSE" title="Software License file">LICENSE</a> file included with this project for the specific language governing permissions and limitations under the <b>License</b>.</p>
    <br>
</div>

<div align="center">
    <h2></h2>
    <sup>COPYRIGHT <small>&copy;</small> 2025 <strong>JAMES GOBER.</strong></sup>
</div>
