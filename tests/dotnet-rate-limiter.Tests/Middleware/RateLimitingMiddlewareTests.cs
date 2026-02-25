using FluentAssertions;
using JG.RateLimiter.Middleware;
using JG.RateLimiter.Policies;
using JG.RateLimiter.Storage;
using JG.RateLimiter.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace JG.RateLimiter.Tests.Middleware;

public sealed class RateLimitingMiddlewareTests : IDisposable
{
    private readonly FakeTimeProvider _time = new();
    private readonly InMemoryRateLimitStore _store;

    public RateLimitingMiddlewareTests()
    {
        _store = new InMemoryRateLimitStore(_time, TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task InvokeAsync_WithinLimit_CallsNextAndSetsHeaders()
    {
        bool nextCalled = false;
        RateLimitingMiddleware middleware = CreateMiddleware(
            CreateOptions("default", new FixedWindowPolicy { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }),
            next: _ => { nextCalled = true; return Task.CompletedTask; });

        HttpContext context = CreateHttpContext();

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        context.Response.Headers.Should().ContainKey("X-RateLimit-Limit");
        context.Response.Headers.Should().ContainKey("X-RateLimit-Remaining");
    }

    [Fact]
    public async Task InvokeAsync_ExceedsLimit_Returns429WithRetryAfter()
    {
        RateLimitingOptions options = CreateOptions("default",
            new FixedWindowPolicy { PermitLimit = 1, Window = TimeSpan.FromMinutes(1) });
        RateLimitingMiddleware middleware = CreateMiddleware(options, next: _ => Task.CompletedTask);

        HttpContext ctx1 = CreateHttpContext();
        await middleware.InvokeAsync(ctx1);

        HttpContext ctx2 = CreateHttpContext();
        await middleware.InvokeAsync(ctx2);

        ctx2.Response.StatusCode.Should().Be(429);
        ctx2.Response.Headers.Should().ContainKey("Retry-After");
    }

    [Fact]
    public async Task InvokeAsync_NoPolicyMatch_CallsNextWithoutRateLimiting()
    {
        bool nextCalled = false;
        var options = new RateLimitingOptions(); // No policies at all
        RateLimitingMiddleware middleware = CreateMiddleware(options,
            next: _ => { nextCalled = true; return Task.CompletedTask; });

        HttpContext context = CreateHttpContext();
        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        context.Response.Headers.Should().NotContainKey("X-RateLimit-Limit");
    }

    [Fact]
    public async Task InvokeAsync_WithEndpointAttribute_UsesCorrectPolicy()
    {
        var options = new RateLimitingOptions();
        options.AddPolicy("strict", new FixedWindowPolicy { PermitLimit = 1, Window = TimeSpan.FromMinutes(1) });
        options.AddPolicy("lenient", new FixedWindowPolicy { PermitLimit = 100, Window = TimeSpan.FromMinutes(1) });

        RateLimitingMiddleware middleware = CreateMiddleware(options, next: _ => Task.CompletedTask);

        // First request uses the "strict" policy via endpoint attribute.
        HttpContext ctx1 = CreateHttpContext(policyName: "strict");
        await middleware.InvokeAsync(ctx1);
        ctx1.Response.StatusCode.Should().Be(200);

        // Second request should be rejected (limit = 1).
        HttpContext ctx2 = CreateHttpContext(policyName: "strict");
        await middleware.InvokeAsync(ctx2);
        ctx2.Response.StatusCode.Should().Be(429);
    }

    [Fact]
    public async Task InvokeAsync_OnRejectedCallback_IsInvoked()
    {
        bool callbackInvoked = false;
        var options = CreateOptions("default",
            new FixedWindowPolicy { PermitLimit = 1, Window = TimeSpan.FromMinutes(1) });
        options.OnRejected = (ctx, lease, ct) =>
        {
            callbackInvoked = true;
            ctx.Response.StatusCode = 503;
            return ValueTask.CompletedTask;
        };

        RateLimitingMiddleware middleware = CreateMiddleware(options, next: _ => Task.CompletedTask);

        await middleware.InvokeAsync(CreateHttpContext());
        await middleware.InvokeAsync(CreateHttpContext());

        callbackInvoked.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_DisableRateLimitingAttribute_BypassesAllPolicies()
    {
        var options = CreateOptions("default",
            new FixedWindowPolicy { PermitLimit = 1, Window = TimeSpan.FromMinutes(1) });
        RateLimitingMiddleware middleware = CreateMiddleware(options, next: _ => Task.CompletedTask);

        // First request exhausts the single permit.
        await middleware.InvokeAsync(CreateHttpContext());

        // Second request has [DisableRateLimiting] — should pass through even though limit is hit.
        bool nextCalled = false;
        RateLimitingMiddleware bypassMiddleware = CreateMiddleware(options,
            next: _ => { nextCalled = true; return Task.CompletedTask; });

        HttpContext disabledCtx = CreateHttpContextWithDisableAttribute();
        await bypassMiddleware.InvokeAsync(disabledCtx);

        nextCalled.Should().BeTrue();
        disabledCtx.Response.Headers.Should().NotContainKey("X-RateLimit-Limit");
    }

    [Fact]
    public async Task InvokeAsync_PartitionByThrows_FallsBackToGlobalPartition()
    {
        var policy = new FixedWindowPolicy
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            PartitionBy = _ => throw new InvalidOperationException("Boom"),
        };
        var options = CreateOptions("default", policy);
        bool nextCalled = false;
        RateLimitingMiddleware middleware = CreateMiddleware(options,
            next: _ => { nextCalled = true; return Task.CompletedTask; });

        HttpContext context = CreateHttpContext();
        await middleware.InvokeAsync(context);

        // Should have gracefully fallen back to global partition and still rate-limited.
        nextCalled.Should().BeTrue();
        context.Response.Headers.Should().ContainKey("X-RateLimit-Limit");
    }

    [Fact]
    public async Task InvokeAsync_CustomRejectionStatusCode_UsesConfiguredCode()
    {
        var options = CreateOptions("default",
            new FixedWindowPolicy { PermitLimit = 1, Window = TimeSpan.FromMinutes(1) });
        options.RejectionStatusCode = 503;

        RateLimitingMiddleware middleware = CreateMiddleware(options, next: _ => Task.CompletedTask);

        await middleware.InvokeAsync(CreateHttpContext());
        HttpContext ctx2 = CreateHttpContext();
        await middleware.InvokeAsync(ctx2);

        ctx2.Response.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task InvokeAsync_HeaderValues_AreCorrectNumbers()
    {
        var options = CreateOptions("default",
            new FixedWindowPolicy { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) });
        RateLimitingMiddleware middleware = CreateMiddleware(options, next: _ => Task.CompletedTask);

        HttpContext context = CreateHttpContext();
        await middleware.InvokeAsync(context);

        context.Response.Headers["X-RateLimit-Limit"].ToString().Should().Be("5");
        context.Response.Headers["X-RateLimit-Remaining"].ToString().Should().Be("4");
    }

    [Fact]
    public async Task InvokeAsync_OnRejectedThrows_StillSetsStatusCode()
    {
        var options = CreateOptions("default",
            new FixedWindowPolicy { PermitLimit = 1, Window = TimeSpan.FromMinutes(1) });
        options.OnRejected = (_, _, _) => throw new InvalidOperationException("callback failed");

        RateLimitingMiddleware middleware = CreateMiddleware(options, next: _ => Task.CompletedTask);

        await middleware.InvokeAsync(CreateHttpContext());
        HttpContext ctx2 = CreateHttpContext();

        // Should not throw; middleware catches it and sets status code.
        Func<Task> act = () => middleware.InvokeAsync(ctx2);
        await act.Should().NotThrowAsync();

        ctx2.Response.StatusCode.Should().Be(429);
    }

    [Fact]
    public async Task InvokeAsync_NoEndpointResolved_UsesDefaultPolicy()
    {
        var options = CreateOptions("default",
            new FixedWindowPolicy { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) });
        RateLimitingMiddleware middleware = CreateMiddleware(options, next: _ => Task.CompletedTask);

        // No endpoint set — middleware should use DefaultPolicyName.
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;

        await middleware.InvokeAsync(context);

        context.Response.Headers.Should().ContainKey("X-RateLimit-Limit");
    }

    [Fact]
    public async Task InvokeAsync_MultiplePolicies_EachEndpointUsesOwnLimiter()
    {
        var options = new RateLimitingOptions();
        options.AddPolicy("p1", new FixedWindowPolicy { PermitLimit = 1, Window = TimeSpan.FromMinutes(1) });
        options.AddPolicy("p2", new FixedWindowPolicy { PermitLimit = 1, Window = TimeSpan.FromMinutes(1) });

        RateLimitingMiddleware middleware = CreateMiddleware(options, next: _ => Task.CompletedTask);

        // Exhaust policy p1.
        await middleware.InvokeAsync(CreateHttpContext(policyName: "p1"));
        HttpContext p1Rejected = CreateHttpContext(policyName: "p1");
        await middleware.InvokeAsync(p1Rejected);
        p1Rejected.Response.StatusCode.Should().Be(429);

        // Policy p2 should still have capacity.
        HttpContext p2Ok = CreateHttpContext(policyName: "p2");
        await middleware.InvokeAsync(p2Ok);
        p2Ok.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task InvokeAsync_PartitionByReturnsEmpty_UsesGlobalPartition()
    {
        var policy = new FixedWindowPolicy
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            PartitionBy = _ => "",
        };
        var options = CreateOptions("default", policy);
        RateLimitingMiddleware middleware = CreateMiddleware(options, next: _ => Task.CompletedTask);

        HttpContext context = CreateHttpContext();
        await middleware.InvokeAsync(context);

        // Should still rate-limit (global partition), not crash.
        context.Response.Headers.Should().ContainKey("X-RateLimit-Limit");
    }

    private RateLimitingMiddleware CreateMiddleware(RateLimitingOptions options, RequestDelegate next)
    {
        IOptions<RateLimitingOptions> wrappedOptions = Options.Create(options);
        var logger = NullLogger<RateLimitingMiddleware>.Instance;
        return new RateLimitingMiddleware(next, wrappedOptions, _store, _time, logger);
    }

    private static RateLimitingOptions CreateOptions(string defaultPolicy, IRateLimiterPolicy policy)
    {
        var options = new RateLimitingOptions { DefaultPolicyName = defaultPolicy };
        options.AddPolicy(defaultPolicy, policy);
        return options;
    }

    private static HttpContext CreateHttpContext(string? policyName = null)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;

        if (policyName is not null)
        {
            var endpoint = new Endpoint(
                requestDelegate: null,
                metadata: new EndpointMetadataCollection(new RateLimitAttribute(policyName)),
                displayName: "test");
            context.SetEndpoint(endpoint);
        }

        return context;
    }

    private static HttpContext CreateHttpContextWithDisableAttribute()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;

        var endpoint = new Endpoint(
            requestDelegate: null,
            metadata: new EndpointMetadataCollection(new DisableRateLimitingAttribute()),
            displayName: "test-disabled");
        context.SetEndpoint(endpoint);

        return context;
    }

    public void Dispose()
    {
        _store.Dispose();
    }
}
