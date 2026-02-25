using FluentAssertions;
using JG.RateLimiter.Algorithms;
using JG.RateLimiter.Tests.Helpers;

namespace JG.RateLimiter.Tests.Algorithms;

public sealed class FixedWindowRateLimiterTests
{
    private readonly FakeTimeProvider _time = new();

    [Fact]
    public void AttemptAcquire_WithinLimit_ReturnsAcquired()
    {
        using var limiter = new FixedWindowRateLimiter(permitLimit: 5, window: TimeSpan.FromSeconds(10), _time);

        using RateLimitLease lease = limiter.AttemptAcquire();

        lease.IsAcquired.Should().BeTrue();
        lease.Remaining.Should().Be(4);
        lease.Limit.Should().Be(5);
    }

    [Fact]
    public void AttemptAcquire_ExceedsLimit_ReturnsRejected()
    {
        using var limiter = new FixedWindowRateLimiter(permitLimit: 2, window: TimeSpan.FromSeconds(10), _time);

        limiter.AttemptAcquire();
        limiter.AttemptAcquire();
        using RateLimitLease lease = limiter.AttemptAcquire();

        lease.IsAcquired.Should().BeFalse();
        lease.RetryAfter.Should().NotBeNull();
    }

    [Fact]
    public void AttemptAcquire_AfterWindowResets_AllowsAgain()
    {
        using var limiter = new FixedWindowRateLimiter(permitLimit: 1, window: TimeSpan.FromSeconds(5), _time);

        limiter.AttemptAcquire();
        limiter.AttemptAcquire().IsAcquired.Should().BeFalse();

        _time.Advance(TimeSpan.FromSeconds(6));
        using RateLimitLease lease = limiter.AttemptAcquire();

        lease.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public void AttemptAcquire_MultiplePermits_ConsumesCorrectly()
    {
        using var limiter = new FixedWindowRateLimiter(permitLimit: 10, window: TimeSpan.FromSeconds(10), _time);

        using RateLimitLease lease = limiter.AttemptAcquire(permitCount: 7);

        lease.IsAcquired.Should().BeTrue();
        lease.Remaining.Should().Be(3);
    }

    [Fact]
    public void AttemptAcquire_ZeroPermits_Throws()
    {
        using var limiter = new FixedWindowRateLimiter(permitLimit: 5, window: TimeSpan.FromSeconds(10), _time);

        Action act = () => limiter.AttemptAcquire(permitCount: 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_InvalidPermitLimit_Throws()
    {
        Action act = () => new FixedWindowRateLimiter(permitLimit: 0, window: TimeSpan.FromSeconds(1), _time);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_NegativeWindow_Throws()
    {
        Action act = () => new FixedWindowRateLimiter(permitLimit: 10, window: TimeSpan.FromSeconds(-1), _time);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GetStatistics_ReturnsCorrectCounts()
    {
        using var limiter = new FixedWindowRateLimiter(permitLimit: 2, window: TimeSpan.FromSeconds(10), _time);

        limiter.AttemptAcquire();
        limiter.AttemptAcquire();
        limiter.AttemptAcquire(); // rejected

        RateLimitStatistics stats = limiter.GetStatistics();

        stats.TotalSuccessfulLeases.Should().Be(2);
        stats.TotalFailedLeases.Should().Be(1);
        stats.CurrentAvailablePermits.Should().Be(0);
    }

    [Fact]
    public void Dispose_PreventsSubsequentAcquire()
    {
        var limiter = new FixedWindowRateLimiter(permitLimit: 10, window: TimeSpan.FromSeconds(10), _time);
        limiter.Dispose();

        Action act = () => limiter.AttemptAcquire();

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void GetStatistics_AfterWindowReset_ShowsFullCapacity()
    {
        using var limiter = new FixedWindowRateLimiter(permitLimit: 5, window: TimeSpan.FromSeconds(10), _time);

        limiter.AttemptAcquire();
        limiter.AttemptAcquire();
        _time.Advance(TimeSpan.FromSeconds(11));

        RateLimitStatistics stats = limiter.GetStatistics();

        stats.CurrentAvailablePermits.Should().Be(5);
    }
}
