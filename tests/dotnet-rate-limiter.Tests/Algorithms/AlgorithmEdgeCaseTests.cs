using FluentAssertions;
using JG.RateLimiter.Algorithms;
using JG.RateLimiter.Tests.Helpers;

namespace JG.RateLimiter.Tests.Algorithms;

/// <summary>
/// Edge case tests that verify boundary conditions, overflow protection,
/// and async paths across all algorithm implementations.
/// </summary>
public sealed class AlgorithmEdgeCaseTests
{
    private readonly FakeTimeProvider _time = new();

    // --- AcquireAsync happy path (all algorithms) ---

    [Fact]
    public async Task TokenBucket_AcquireAsync_HappyPath_ReturnsAcquired()
    {
        using var limiter = new TokenBucketRateLimiter(
            permitLimit: 10, window: TimeSpan.FromSeconds(10), burstLimit: 5, _time);

        using RateLimitLease lease = await limiter.AcquireAsync();

        lease.IsAcquired.Should().BeTrue();
        lease.Remaining.Should().Be(4);
    }

    [Fact]
    public async Task FixedWindow_AcquireAsync_HappyPath_ReturnsAcquired()
    {
        using var limiter = new FixedWindowRateLimiter(
            permitLimit: 10, window: TimeSpan.FromSeconds(10), _time);

        using RateLimitLease lease = await limiter.AcquireAsync();

        lease.IsAcquired.Should().BeTrue();
        lease.Remaining.Should().Be(9);
    }

    [Fact]
    public async Task SlidingWindow_AcquireAsync_HappyPath_ReturnsAcquired()
    {
        using var limiter = new SlidingWindowRateLimiter(
            permitLimit: 10, window: TimeSpan.FromSeconds(10), segmentsPerWindow: 3, _time);

        using RateLimitLease lease = await limiter.AcquireAsync();

        lease.IsAcquired.Should().BeTrue();
        lease.Remaining.Should().Be(9);
    }

    [Fact]
    public async Task Concurrency_AcquireAsync_HappyPath_ReturnsAcquired()
    {
        using var limiter = new ConcurrencyLimiter(permitLimit: 5);

        using RateLimitLease lease = await limiter.AcquireAsync();

        lease.IsAcquired.Should().BeTrue();
        lease.Remaining.Should().Be(4);
    }

    // --- PermitCount exceeding the total limit always fails ---

    [Fact]
    public void TokenBucket_AttemptAcquire_PermitCountExceedsLimit_AlwaysFails()
    {
        using var limiter = new TokenBucketRateLimiter(
            permitLimit: 10, window: TimeSpan.FromSeconds(10), burstLimit: 5, _time);

        using RateLimitLease lease = limiter.AttemptAcquire(permitCount: 6);

        lease.IsAcquired.Should().BeFalse();
    }

    [Fact]
    public void FixedWindow_AttemptAcquire_PermitCountExceedsLimit_AlwaysFails()
    {
        using var limiter = new FixedWindowRateLimiter(
            permitLimit: 5, window: TimeSpan.FromSeconds(10), _time);

        using RateLimitLease lease = limiter.AttemptAcquire(permitCount: 6);

        lease.IsAcquired.Should().BeFalse();
    }

    [Fact]
    public void SlidingWindow_AttemptAcquire_PermitCountExceedsLimit_AlwaysFails()
    {
        using var limiter = new SlidingWindowRateLimiter(
            permitLimit: 5, window: TimeSpan.FromSeconds(10), segmentsPerWindow: 2, _time);

        using RateLimitLease lease = limiter.AttemptAcquire(permitCount: 6);

        lease.IsAcquired.Should().BeFalse();
    }

    [Fact]
    public void Concurrency_AttemptAcquire_PermitCountExceedsLimit_AlwaysFails()
    {
        using var limiter = new ConcurrencyLimiter(permitLimit: 5);

        using RateLimitLease lease = limiter.AttemptAcquire(permitCount: 6);

        lease.IsAcquired.Should().BeFalse();
    }

    // --- Integer overflow protection ---

    [Fact]
    public void FixedWindow_AttemptAcquire_LargePermitCount_DoesNotOverflow()
    {
        using var limiter = new FixedWindowRateLimiter(
            permitLimit: 10, window: TimeSpan.FromSeconds(10), _time);

        limiter.AttemptAcquire(1); // requestCount = 1

        // int.MaxValue + 1 would overflow to negative without the long cast.
        using RateLimitLease lease = limiter.AttemptAcquire(permitCount: int.MaxValue);

        lease.IsAcquired.Should().BeFalse();
    }

    [Fact]
    public void SlidingWindow_AttemptAcquire_LargePermitCount_DoesNotOverflow()
    {
        using var limiter = new SlidingWindowRateLimiter(
            permitLimit: 10, window: TimeSpan.FromSeconds(10), segmentsPerWindow: 2, _time);

        limiter.AttemptAcquire(1);

        using RateLimitLease lease = limiter.AttemptAcquire(permitCount: int.MaxValue);

        lease.IsAcquired.Should().BeFalse();
    }

    [Fact]
    public void Concurrency_AttemptAcquire_LargePermitCount_DoesNotOverflow()
    {
        using var limiter = new ConcurrencyLimiter(permitLimit: 10);

        using var held = limiter.AttemptAcquire(1);

        using RateLimitLease lease = limiter.AttemptAcquire(permitCount: int.MaxValue);

        lease.IsAcquired.Should().BeFalse();
    }

    // --- DisposeAsync works on all algorithms ---

    [Fact]
    public async Task TokenBucket_DisposeAsync_PreventsSubsequentAcquire()
    {
        var limiter = new TokenBucketRateLimiter(
            permitLimit: 10, window: TimeSpan.FromSeconds(10), burstLimit: 5, _time);

        await limiter.DisposeAsync();

        Action act = () => limiter.AttemptAcquire();
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task FixedWindow_DisposeAsync_PreventsSubsequentAcquire()
    {
        var limiter = new FixedWindowRateLimiter(
            permitLimit: 10, window: TimeSpan.FromSeconds(10), _time);

        await limiter.DisposeAsync();

        Action act = () => limiter.AttemptAcquire();
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task SlidingWindow_DisposeAsync_PreventsSubsequentAcquire()
    {
        var limiter = new SlidingWindowRateLimiter(
            permitLimit: 10, window: TimeSpan.FromSeconds(10), segmentsPerWindow: 2, _time);

        await limiter.DisposeAsync();

        Action act = () => limiter.AttemptAcquire();
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task Concurrency_DisposeAsync_PreventsSubsequentAcquire()
    {
        var limiter = new ConcurrencyLimiter(permitLimit: 5);

        await limiter.DisposeAsync();

        Action act = () => limiter.AttemptAcquire();
        act.Should().Throw<ObjectDisposedException>();
    }

    // --- GetStatistics throws after dispose ---

    [Fact]
    public void TokenBucket_GetStatistics_AfterDispose_Throws()
    {
        var limiter = new TokenBucketRateLimiter(
            permitLimit: 10, window: TimeSpan.FromSeconds(10), burstLimit: 5, _time);
        limiter.Dispose();

        Action act = () => limiter.GetStatistics();
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void FixedWindow_GetStatistics_AfterDispose_Throws()
    {
        var limiter = new FixedWindowRateLimiter(
            permitLimit: 10, window: TimeSpan.FromSeconds(10), _time);
        limiter.Dispose();

        Action act = () => limiter.GetStatistics();
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void SlidingWindow_GetStatistics_AfterDispose_Throws()
    {
        var limiter = new SlidingWindowRateLimiter(
            permitLimit: 10, window: TimeSpan.FromSeconds(10), segmentsPerWindow: 2, _time);
        limiter.Dispose();

        Action act = () => limiter.GetStatistics();
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Concurrency_GetStatistics_AfterDispose_Throws()
    {
        var limiter = new ConcurrencyLimiter(permitLimit: 5);
        limiter.Dispose();

        Action act = () => limiter.GetStatistics();
        act.Should().Throw<ObjectDisposedException>();
    }

    // --- Multiple rapid window resets ---

    [Fact]
    public void FixedWindow_MultipleWindowResets_CountsResetEachTime()
    {
        using var limiter = new FixedWindowRateLimiter(
            permitLimit: 2, window: TimeSpan.FromSeconds(5), _time);

        for (int window = 0; window < 3; window++)
        {
            limiter.AttemptAcquire().IsAcquired.Should().BeTrue();
            limiter.AttemptAcquire().IsAcquired.Should().BeTrue();
            limiter.AttemptAcquire().IsAcquired.Should().BeFalse();
            _time.Advance(TimeSpan.FromSeconds(6));
        }

        limiter.GetStatistics().TotalSuccessfulLeases.Should().Be(6);
        limiter.GetStatistics().TotalFailedLeases.Should().Be(3);
    }

    // --- Token bucket: exact single token left ---

    [Fact]
    public void TokenBucket_ExactlyOneTokenLeft_AcquiresOneRejectsTwo()
    {
        using var limiter = new TokenBucketRateLimiter(
            permitLimit: 10, window: TimeSpan.FromSeconds(10), burstLimit: 3, _time);

        limiter.AttemptAcquire(2).IsAcquired.Should().BeTrue(); // 1 left
        limiter.AttemptAcquire(1).IsAcquired.Should().BeTrue(); // 0 left
        limiter.AttemptAcquire(1).IsAcquired.Should().BeFalse();
    }

    // --- RetryAfter is positive on rejection ---

    [Fact]
    public void TokenBucket_RetryAfter_IsPositiveOnRejection()
    {
        using var limiter = new TokenBucketRateLimiter(
            permitLimit: 10, window: TimeSpan.FromSeconds(10), burstLimit: 1, _time);

        limiter.AttemptAcquire();
        using RateLimitLease lease = limiter.AttemptAcquire();

        lease.IsAcquired.Should().BeFalse();
        lease.RetryAfter.Should().NotBeNull();
        lease.RetryAfter!.Value.TotalMilliseconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public void FixedWindow_RetryAfter_IsPositiveOnRejection()
    {
        using var limiter = new FixedWindowRateLimiter(
            permitLimit: 1, window: TimeSpan.FromSeconds(10), _time);

        limiter.AttemptAcquire();
        using RateLimitLease lease = limiter.AttemptAcquire();

        lease.IsAcquired.Should().BeFalse();
        lease.RetryAfter.Should().NotBeNull();
        lease.RetryAfter!.Value.TotalMilliseconds.Should().BeGreaterThan(0);
    }

    // --- ResetAfter is positive on acquisition ---

    [Fact]
    public void FixedWindow_ResetAfter_IsPositiveOnAcquisition()
    {
        using var limiter = new FixedWindowRateLimiter(
            permitLimit: 10, window: TimeSpan.FromSeconds(30), _time);

        using RateLimitLease lease = limiter.AttemptAcquire();

        lease.IsAcquired.Should().BeTrue();
        lease.ResetAfter.Should().NotBeNull();
        lease.ResetAfter!.Value.TotalSeconds.Should().BeGreaterThan(0);
    }
}
