using FluentAssertions;
using JG.RateLimiter.Policies;
using JG.RateLimiter.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using System.Net;

namespace JG.RateLimiter.Tests.Policies;

public sealed class TokenBucketPolicyTests
{
    [Fact]
    public void CreateLimiter_ReturnsConfiguredLimiter()
    {
        var policy = new TokenBucketPolicy
        {
            PermitLimit = 50,
            Window = TimeSpan.FromMinutes(1),
            BurstLimit = 10,
        };

        IRateLimiter limiter = ((IRateLimiterPolicy)policy).CreateLimiter(TimeProvider.System);

        limiter.Should().NotBeNull();
        limiter.GetStatistics().CurrentAvailablePermits.Should().Be(10);
        limiter.Dispose();
    }

    [Fact]
    public void PartitionBy_DefaultsToRemoteIpAddress()
    {
        var policy = new TokenBucketPolicy
        {
            PermitLimit = 10,
            Window = TimeSpan.FromSeconds(10),
            BurstLimit = 5,
        };

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.42");

        string? key = policy.PartitionBy(context);

        key.Should().Be("192.168.1.42");
    }

    [Fact]
    public void PartitionBy_CustomDelegate_IsUsed()
    {
        var policy = new TokenBucketPolicy
        {
            PermitLimit = 10,
            Window = TimeSpan.FromSeconds(10),
            BurstLimit = 5,
            PartitionBy = ctx => ctx.Request.Headers["X-Api-Key"].ToString(),
        };

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Api-Key"] = "test-key-123";

        string? key = policy.PartitionBy(context);

        key.Should().Be("test-key-123");
    }
}

public sealed class FixedWindowPolicyTests
{
    [Fact]
    public void CreateLimiter_ReturnsConfiguredLimiter()
    {
        var policy = new FixedWindowPolicy
        {
            PermitLimit = 100,
            Window = TimeSpan.FromMinutes(5),
        };

        IRateLimiter limiter = ((IRateLimiterPolicy)policy).CreateLimiter(TimeProvider.System);

        limiter.Should().NotBeNull();
        limiter.GetStatistics().CurrentAvailablePermits.Should().Be(100);
        limiter.Dispose();
    }

    [Fact]
    public void PartitionBy_DefaultsToRemoteIpAddress()
    {
        var policy = new FixedWindowPolicy
        {
            PermitLimit = 10,
            Window = TimeSpan.FromSeconds(10),
        };

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Loopback;

        string? key = policy.PartitionBy(context);

        key.Should().Be("127.0.0.1");
    }
}

public sealed class SlidingWindowPolicyTests
{
    [Fact]
    public void CreateLimiter_ReturnsConfiguredLimiter()
    {
        var policy = new SlidingWindowPolicy
        {
            PermitLimit = 200,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6,
        };

        IRateLimiter limiter = ((IRateLimiterPolicy)policy).CreateLimiter(TimeProvider.System);

        limiter.Should().NotBeNull();
        limiter.GetStatistics().CurrentAvailablePermits.Should().Be(200);
        limiter.Dispose();
    }

    [Fact]
    public void SegmentsPerWindow_DefaultsToThree()
    {
        var policy = new SlidingWindowPolicy
        {
            PermitLimit = 10,
            Window = TimeSpan.FromSeconds(10),
        };

        policy.SegmentsPerWindow.Should().Be(3);
    }
}

public sealed class ConcurrencyPolicyTests
{
    [Fact]
    public void CreateLimiter_ReturnsConfiguredLimiter()
    {
        var policy = new ConcurrencyPolicy
        {
            PermitLimit = 25,
        };

        IRateLimiter limiter = ((IRateLimiterPolicy)policy).CreateLimiter(TimeProvider.System);

        limiter.Should().NotBeNull();
        limiter.GetStatistics().CurrentAvailablePermits.Should().Be(25);
        limiter.Dispose();
    }
}
