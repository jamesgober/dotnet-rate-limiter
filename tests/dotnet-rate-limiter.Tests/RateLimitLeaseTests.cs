using FluentAssertions;

namespace JG.RateLimiter.Tests;

public sealed class RateLimitLeaseTests
{
    [Fact]
    public void Acquired_Lease_HasCorrectProperties()
    {
        using RateLimitLease lease = RateLimitLease.Acquired(
            limit: 100, remaining: 42, resetAfter: TimeSpan.FromSeconds(30));

        lease.IsAcquired.Should().BeTrue();
        lease.Limit.Should().Be(100);
        lease.Remaining.Should().Be(42);
        lease.ResetAfter.Should().Be(TimeSpan.FromSeconds(30));
        lease.RetryAfter.Should().BeNull();
    }

    [Fact]
    public void Rejected_Lease_HasCorrectProperties()
    {
        using RateLimitLease lease = RateLimitLease.Rejected(
            limit: 100, retryAfter: TimeSpan.FromSeconds(5));

        lease.IsAcquired.Should().BeFalse();
        lease.Limit.Should().Be(100);
        lease.Remaining.Should().Be(0);
        lease.RetryAfter.Should().Be(TimeSpan.FromSeconds(5));
        lease.ResetAfter.Should().BeNull();
    }

    [Fact]
    public void Dispose_InvokesOnDisposeCallback()
    {
        bool called = false;
        RateLimitLease lease = RateLimitLease.Acquired(
            limit: 1, remaining: 0, resetAfter: null, onDispose: () => called = true);

        lease.Dispose();

        called.Should().BeTrue();
    }

    [Fact]
    public void Dispose_CalledTwice_InvokesCallbackOnlyOnce()
    {
        int callCount = 0;
        RateLimitLease lease = RateLimitLease.Acquired(
            limit: 1, remaining: 0, resetAfter: null, onDispose: () => callCount++);

        lease.Dispose();
        lease.Dispose();

        callCount.Should().Be(1);
    }

    [Fact]
    public void Dispose_WithoutCallback_DoesNotThrow()
    {
        RateLimitLease lease = RateLimitLease.Rejected(limit: 10, retryAfter: null);

        Action act = () => lease.Dispose();

        act.Should().NotThrow();
    }
}
