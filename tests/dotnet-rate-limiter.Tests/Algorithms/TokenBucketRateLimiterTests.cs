using FluentAssertions;
using JG.RateLimiter.Algorithms;
using JG.RateLimiter.Tests.Helpers;

namespace JG.RateLimiter.Tests.Algorithms;

public sealed class TokenBucketRateLimiterTests
{
    private readonly FakeTimeProvider _time = new();

    [Fact]
    public void AttemptAcquire_WithAvailableTokens_ReturnsAcquiredLease()
    {
        using var limiter = new TokenBucketRateLimiter(
            permitLimit: 10, window: TimeSpan.FromSeconds(10), burstLimit: 10, _time);

        using RateLimitLease lease = limiter.AttemptAcquire();

        lease.IsAcquired.Should().BeTrue();
        lease.Remaining.Should().Be(9);
        lease.Limit.Should().Be(10);
    }

    [Fact]
    public void AttemptAcquire_ExhaustedTokens_ReturnsRejectedLease()
    {
        using var limiter = new TokenBucketRateLimiter(
            permitLimit: 10, window: TimeSpan.FromSeconds(10), burstLimit: 2, _time);

        using var _ = limiter.AttemptAcquire();
        using var __ = limiter.AttemptAcquire();
        using RateLimitLease lease = limiter.AttemptAcquire();

        lease.IsAcquired.Should().BeFalse();
        lease.RetryAfter.Should().NotBeNull();
        lease.RetryAfter!.Value.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void AttemptAcquire_AfterRefill_ReturnsAcquiredLease()
    {
        using var limiter = new TokenBucketRateLimiter(
            permitLimit: 10, window: TimeSpan.FromSeconds(10), burstLimit: 1, _time);

        using var _ = limiter.AttemptAcquire();
        limiter.AttemptAcquire().IsAcquired.Should().BeFalse();

        _time.Advance(TimeSpan.FromSeconds(2));
        using RateLimitLease lease = limiter.AttemptAcquire();

        lease.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public void AttemptAcquire_BurstCapacity_DoesNotExceedMaxTokens()
    {
        using var limiter = new TokenBucketRateLimiter(
            permitLimit: 100, window: TimeSpan.FromSeconds(10), burstLimit: 5, _time);

        // Wait a long time — tokens should cap at burstLimit.
        _time.Advance(TimeSpan.FromMinutes(5));

        int acquired = 0;
        for (int i = 0; i < 10; i++)
        {
            using RateLimitLease lease = limiter.AttemptAcquire();
            if (lease.IsAcquired)
            {
                acquired++;
            }
        }

        acquired.Should().Be(5);
    }

    [Fact]
    public void AttemptAcquire_MultiplePermits_ConsumesCorrectAmount()
    {
        using var limiter = new TokenBucketRateLimiter(
            permitLimit: 10, window: TimeSpan.FromSeconds(10), burstLimit: 10, _time);

        using RateLimitLease lease = limiter.AttemptAcquire(permitCount: 7);

        lease.IsAcquired.Should().BeTrue();
        lease.Remaining.Should().Be(3);
    }

    [Fact]
    public void AttemptAcquire_ZeroPermits_ThrowsArgumentOutOfRange()
    {
        using var limiter = new TokenBucketRateLimiter(
            permitLimit: 10, window: TimeSpan.FromSeconds(10), burstLimit: 10, _time);

        Action act = () => limiter.AttemptAcquire(permitCount: 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_InvalidPermitLimit_ThrowsArgumentOutOfRange()
    {
        Action act = () => new TokenBucketRateLimiter(
            permitLimit: 0, window: TimeSpan.FromSeconds(1), burstLimit: 1, _time);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_InvalidWindow_ThrowsArgumentOutOfRange()
    {
        Action act = () => new TokenBucketRateLimiter(
            permitLimit: 10, window: TimeSpan.Zero, burstLimit: 1, _time);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_InvalidBurstLimit_ThrowsArgumentOutOfRange()
    {
        Action act = () => new TokenBucketRateLimiter(
            permitLimit: 10, window: TimeSpan.FromSeconds(1), burstLimit: 0, _time);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GetStatistics_ReturnsCorrectCounts()
    {
        using var limiter = new TokenBucketRateLimiter(
            permitLimit: 10, window: TimeSpan.FromSeconds(10), burstLimit: 2, _time);

        using var _ = limiter.AttemptAcquire();
        using var __ = limiter.AttemptAcquire();
        limiter.AttemptAcquire(); // rejected

        RateLimitStatistics stats = limiter.GetStatistics();

        stats.TotalSuccessfulLeases.Should().Be(2);
        stats.TotalFailedLeases.Should().Be(1);
        stats.CurrentAvailablePermits.Should().Be(0);
    }

    [Fact]
    public void Dispose_PreventsSubsequentAcquire()
    {
        var limiter = new TokenBucketRateLimiter(
            permitLimit: 10, window: TimeSpan.FromSeconds(10), burstLimit: 10, _time);

        limiter.Dispose();

        Action act = () => limiter.AttemptAcquire();
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task AcquireAsync_Cancelled_ThrowsOperationCancelled()
    {
        using var limiter = new TokenBucketRateLimiter(
            permitLimit: 10, window: TimeSpan.FromSeconds(10), burstLimit: 10, _time);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = async () => await limiter.AcquireAsync(cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
