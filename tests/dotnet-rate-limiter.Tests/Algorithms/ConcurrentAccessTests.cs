using FluentAssertions;
using JG.RateLimiter.Algorithms;
using JG.RateLimiter.Tests.Helpers;

namespace JG.RateLimiter.Tests.Algorithms;

/// <summary>
/// Tests that verify algorithms behave correctly under concurrent access.
/// </summary>
public sealed class ConcurrentAccessTests
{
    [Fact]
    public void TokenBucket_ConcurrentAcquire_NeverExceedsBurstLimit()
    {
        var time = new FakeTimeProvider();
        using var limiter = new TokenBucketRateLimiter(
            permitLimit: 100, window: TimeSpan.FromSeconds(10), burstLimit: 50, time);

        int acquired = 0;
        Parallel.For(0, 200, _ =>
        {
            using RateLimitLease lease = limiter.AttemptAcquire();
            if (lease.IsAcquired)
            {
                Interlocked.Increment(ref acquired);
            }
        });

        acquired.Should().BeLessOrEqualTo(50);
        acquired.Should().BeGreaterThan(0);
    }

    [Fact]
    public void FixedWindow_ConcurrentAcquire_NeverExceedsLimit()
    {
        var time = new FakeTimeProvider();
        using var limiter = new FixedWindowRateLimiter(
            permitLimit: 30, window: TimeSpan.FromMinutes(1), time);

        int acquired = 0;
        Parallel.For(0, 200, _ =>
        {
            using RateLimitLease lease = limiter.AttemptAcquire();
            if (lease.IsAcquired)
            {
                Interlocked.Increment(ref acquired);
            }
        });

        acquired.Should().Be(30);
    }

    [Fact]
    public void SlidingWindow_ConcurrentAcquire_NeverExceedsLimit()
    {
        var time = new FakeTimeProvider();
        using var limiter = new SlidingWindowRateLimiter(
            permitLimit: 20, window: TimeSpan.FromMinutes(1), segmentsPerWindow: 4, time);

        int acquired = 0;
        Parallel.For(0, 200, _ =>
        {
            using RateLimitLease lease = limiter.AttemptAcquire();
            if (lease.IsAcquired)
            {
                Interlocked.Increment(ref acquired);
            }
        });

        acquired.Should().Be(20);
    }

    [Fact]
    public void ConcurrencyLimiter_ConcurrentAcquire_NeverExceedsLimit()
    {
        using var limiter = new ConcurrencyLimiter(permitLimit: 10);

        int peakConcurrency = 0;
        int currentConcurrency = 0;

        Parallel.For(0, 100, _ =>
        {
            using RateLimitLease lease = limiter.AttemptAcquire();
            if (lease.IsAcquired)
            {
                int current = Interlocked.Increment(ref currentConcurrency);

                // Track peak to verify we never exceed the limit.
                int peak;
                do
                {
                    peak = Volatile.Read(ref peakConcurrency);
                }
                while (current > peak && Interlocked.CompareExchange(ref peakConcurrency, current, peak) != peak);

                // Simulate some work.
                Thread.SpinWait(100);

                Interlocked.Decrement(ref currentConcurrency);
            }
        });

        peakConcurrency.Should().BeLessOrEqualTo(10);
    }

    [Fact]
    public void ConcurrencyLimiter_ConcurrentAcquireAndRelease_MaintainsCorrectCount()
    {
        using var limiter = new ConcurrencyLimiter(permitLimit: 5);

        // Acquire all 5 permits across threads, release them, then acquire again.
        var leases = new RateLimitLease[5];
        Parallel.For(0, 5, i =>
        {
            leases[i] = limiter.AttemptAcquire();
        });

        foreach (RateLimitLease lease in leases)
        {
            lease.IsAcquired.Should().BeTrue();
        }

        limiter.AttemptAcquire().IsAcquired.Should().BeFalse();

        // Release all.
        foreach (RateLimitLease lease in leases)
        {
            lease.Dispose();
        }

        // All permits should be available again.
        limiter.GetStatistics().CurrentAvailablePermits.Should().Be(5);
    }

    [Fact]
    public void FixedWindow_ConcurrentStatistics_DoNotCorrupt()
    {
        var time = new FakeTimeProvider();
        using var limiter = new FixedWindowRateLimiter(
            permitLimit: 1000, window: TimeSpan.FromMinutes(1), time);

        Parallel.For(0, 500, _ =>
        {
            limiter.AttemptAcquire();
            limiter.GetStatistics();
        });

        RateLimitStatistics stats = limiter.GetStatistics();
        long total = stats.TotalSuccessfulLeases + stats.TotalFailedLeases;

        total.Should().Be(500);
        stats.TotalSuccessfulLeases.Should().Be(500);
    }
}
