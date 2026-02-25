using FluentAssertions;
using JG.RateLimiter.Algorithms;

namespace JG.RateLimiter.Tests.Algorithms;

public sealed class ConcurrencyLimiterTests
{
    [Fact]
    public void AttemptAcquire_UnderLimit_ReturnsAcquired()
    {
        using var limiter = new ConcurrencyLimiter(permitLimit: 5);

        using RateLimitLease lease = limiter.AttemptAcquire();

        lease.IsAcquired.Should().BeTrue();
        lease.Remaining.Should().Be(4);
        lease.Limit.Should().Be(5);
    }

    [Fact]
    public void AttemptAcquire_AtLimit_ReturnsRejected()
    {
        using var limiter = new ConcurrencyLimiter(permitLimit: 2);

        using var lease1 = limiter.AttemptAcquire();
        using var lease2 = limiter.AttemptAcquire();
        using RateLimitLease lease3 = limiter.AttemptAcquire();

        lease3.IsAcquired.Should().BeFalse();
    }

    [Fact]
    public void AttemptAcquire_AfterDispose_PermitReleased()
    {
        using var limiter = new ConcurrencyLimiter(permitLimit: 1);

        RateLimitLease lease1 = limiter.AttemptAcquire();
        limiter.AttemptAcquire().IsAcquired.Should().BeFalse();

        lease1.Dispose();

        using RateLimitLease lease2 = limiter.AttemptAcquire();
        lease2.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public void AttemptAcquire_MultiplePermits_ConsumesCorrectly()
    {
        using var limiter = new ConcurrencyLimiter(permitLimit: 10);

        using RateLimitLease lease = limiter.AttemptAcquire(permitCount: 7);

        lease.IsAcquired.Should().BeTrue();
        lease.Remaining.Should().Be(3);
    }

    [Fact]
    public void AttemptAcquire_MultiplePermits_ExceedsLimit_Rejected()
    {
        using var limiter = new ConcurrencyLimiter(permitLimit: 5);

        using var held = limiter.AttemptAcquire(permitCount: 3);
        using RateLimitLease lease = limiter.AttemptAcquire(permitCount: 3);

        lease.IsAcquired.Should().BeFalse();
    }

    [Fact]
    public void AttemptAcquire_ZeroPermits_Throws()
    {
        using var limiter = new ConcurrencyLimiter(permitLimit: 5);

        Action act = () => limiter.AttemptAcquire(permitCount: 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_InvalidPermitLimit_Throws()
    {
        Action act = () => new ConcurrencyLimiter(permitLimit: 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GetStatistics_ReturnsCorrectCounts()
    {
        using var limiter = new ConcurrencyLimiter(permitLimit: 1);

        using var _ = limiter.AttemptAcquire();
        limiter.AttemptAcquire(); // rejected

        RateLimitStatistics stats = limiter.GetStatistics();

        stats.TotalSuccessfulLeases.Should().Be(1);
        stats.TotalFailedLeases.Should().Be(1);
        stats.CurrentAvailablePermits.Should().Be(0);
    }

    [Fact]
    public void GetStatistics_AfterRelease_ShowsAvailablePermits()
    {
        using var limiter = new ConcurrencyLimiter(permitLimit: 3);

        RateLimitLease lease = limiter.AttemptAcquire(permitCount: 2);
        limiter.GetStatistics().CurrentAvailablePermits.Should().Be(1);

        lease.Dispose();
        limiter.GetStatistics().CurrentAvailablePermits.Should().Be(3);
    }

    [Fact]
    public void Dispose_PreventsSubsequentAcquire()
    {
        var limiter = new ConcurrencyLimiter(permitLimit: 5);
        limiter.Dispose();

        Action act = () => limiter.AttemptAcquire();

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task AcquireAsync_Cancelled_Throws()
    {
        using var limiter = new ConcurrencyLimiter(permitLimit: 5);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = async () => await limiter.AcquireAsync(cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void LeaseDispose_CalledMultipleTimes_ReleasesOnce()
    {
        using var limiter = new ConcurrencyLimiter(permitLimit: 1);

        RateLimitLease lease = limiter.AttemptAcquire();
        lease.Dispose();
        lease.Dispose(); // should not double-release

        limiter.GetStatistics().CurrentAvailablePermits.Should().Be(1);
    }
}
