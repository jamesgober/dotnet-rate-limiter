using FluentAssertions;
using JG.RateLimiter.Algorithms;
using JG.RateLimiter.Tests.Helpers;

namespace JG.RateLimiter.Tests.Algorithms;

public sealed class SlidingWindowRateLimiterTests
{
    private readonly FakeTimeProvider _time = new();

    [Fact]
    public void AttemptAcquire_WithinLimit_ReturnsAcquired()
    {
        using var limiter = new SlidingWindowRateLimiter(
            permitLimit: 10, window: TimeSpan.FromSeconds(10), segmentsPerWindow: 2, _time);

        using RateLimitLease lease = limiter.AttemptAcquire();

        lease.IsAcquired.Should().BeTrue();
        lease.Remaining.Should().Be(9);
    }

    [Fact]
    public void AttemptAcquire_ExceedsLimit_ReturnsRejected()
    {
        using var limiter = new SlidingWindowRateLimiter(
            permitLimit: 2, window: TimeSpan.FromSeconds(10), segmentsPerWindow: 2, _time);

        limiter.AttemptAcquire();
        limiter.AttemptAcquire();
        using RateLimitLease lease = limiter.AttemptAcquire();

        lease.IsAcquired.Should().BeFalse();
    }

    [Fact]
    public void AttemptAcquire_SegmentsSlide_GraduallyFreesCapacity()
    {
        // 5 segments of 2s each over a 10s window, limit 4.
        using var limiter = new SlidingWindowRateLimiter(
            permitLimit: 4, window: TimeSpan.FromSeconds(10), segmentsPerWindow: 5, _time);

        // Add 2 requests in segment 0 (time 0).
        limiter.AttemptAcquire();
        limiter.AttemptAcquire();

        // Advance to segment 2 (4s elapsed).
        _time.Advance(TimeSpan.FromSeconds(4));

        // Add 2 more requests — total across segments = 4, at limit.
        limiter.AttemptAcquire();
        limiter.AttemptAcquire();
        limiter.AttemptAcquire().IsAcquired.Should().BeFalse();

        // Advance 6s more (10s from start). Segment 0's slot is recycled, clearing its 2 counts.
        _time.Advance(TimeSpan.FromSeconds(6));

        // Only segment 2's counts remain (2 of 4), so we can acquire again.
        using RateLimitLease lease = limiter.AttemptAcquire();
        lease.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public void AttemptAcquire_FullWindowExpires_ResetsCompletely()
    {
        using var limiter = new SlidingWindowRateLimiter(
            permitLimit: 2, window: TimeSpan.FromSeconds(10), segmentsPerWindow: 2, _time);

        limiter.AttemptAcquire();
        limiter.AttemptAcquire();

        _time.Advance(TimeSpan.FromSeconds(11));

        RateLimitStatistics stats = limiter.GetStatistics();
        stats.CurrentAvailablePermits.Should().Be(2);
    }

    [Fact]
    public void AttemptAcquire_MultiplePermits_ConsumesCorrectly()
    {
        using var limiter = new SlidingWindowRateLimiter(
            permitLimit: 10, window: TimeSpan.FromSeconds(10), segmentsPerWindow: 2, _time);

        using RateLimitLease lease = limiter.AttemptAcquire(permitCount: 8);

        lease.IsAcquired.Should().BeTrue();
        lease.Remaining.Should().Be(2);
    }

    [Fact]
    public void Constructor_InvalidSegments_Throws()
    {
        Action act = () => new SlidingWindowRateLimiter(
            permitLimit: 10, window: TimeSpan.FromSeconds(10), segmentsPerWindow: 1, _time);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_InvalidPermitLimit_Throws()
    {
        Action act = () => new SlidingWindowRateLimiter(
            permitLimit: 0, window: TimeSpan.FromSeconds(10), segmentsPerWindow: 2, _time);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_InvalidWindow_Throws()
    {
        Action act = () => new SlidingWindowRateLimiter(
            permitLimit: 10, window: TimeSpan.Zero, segmentsPerWindow: 2, _time);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GetStatistics_ReturnsCorrectCounts()
    {
        using var limiter = new SlidingWindowRateLimiter(
            permitLimit: 2, window: TimeSpan.FromSeconds(10), segmentsPerWindow: 2, _time);

        limiter.AttemptAcquire();
        limiter.AttemptAcquire();
        limiter.AttemptAcquire(); // rejected

        RateLimitStatistics stats = limiter.GetStatistics();

        stats.TotalSuccessfulLeases.Should().Be(2);
        stats.TotalFailedLeases.Should().Be(1);
    }

    [Fact]
    public void Dispose_PreventsSubsequentAcquire()
    {
        var limiter = new SlidingWindowRateLimiter(
            permitLimit: 10, window: TimeSpan.FromSeconds(10), segmentsPerWindow: 2, _time);

        limiter.Dispose();

        Action act = () => limiter.AttemptAcquire();
        act.Should().Throw<ObjectDisposedException>();
    }
}
