namespace JG.RateLimiter.Algorithms;

/// <summary>
/// A rate limiter that uses the token bucket algorithm, allowing controlled bursts
/// while enforcing a sustained request rate.
/// </summary>
/// <remarks>
/// <para>
/// Tokens are replenished continuously based on elapsed time. When a request arrives,
/// the bucket is refilled proportionally to the time since the last access, then tokens
/// are consumed. If insufficient tokens are available the request is rejected.
/// </para>
/// <para>Thread-safe. All public members may be called concurrently.</para>
/// </remarks>
public sealed class TokenBucketRateLimiter : IRateLimiter
{
    private readonly int _burstLimit;
    private readonly double _tokensPerMillisecond;
    private readonly TimeSpan _window;
    private readonly TimeProvider _timeProvider;
    private readonly object _lock = new();

    private double _availableTokens;
    private long _lastRefillTimestamp;
    private long _successCount;
    private long _failCount;
    private volatile bool _disposed;

    /// <summary>
    /// Creates a new token bucket rate limiter.
    /// </summary>
    /// <param name="permitLimit">The number of permits replenished per <paramref name="window"/>.</param>
    /// <param name="window">The time period over which <paramref name="permitLimit"/> tokens are added.</param>
    /// <param name="burstLimit">The maximum number of tokens the bucket can hold (burst capacity).</param>
    /// <param name="timeProvider">
    /// Optional time provider for testing. Defaults to <see cref="TimeProvider.System"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="permitLimit"/> or <paramref name="burstLimit"/> is less than 1,
    /// or <paramref name="window"/> is not a positive duration.
    /// </exception>
    public TokenBucketRateLimiter(int permitLimit, TimeSpan window, int burstLimit, TimeProvider? timeProvider = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(permitLimit, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(burstLimit, 1);

        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window), window, "Window must be a positive duration.");
        }

        _burstLimit = burstLimit;
        _window = window;
        _tokensPerMillisecond = permitLimit / window.TotalMilliseconds;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _availableTokens = burstLimit;
        _lastRefillTimestamp = _timeProvider.GetTimestamp();
    }

    /// <inheritdoc />
    public RateLimitLease AttemptAcquire(int permitCount = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(permitCount, 1);
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            RefillTokens();

            if (_availableTokens >= permitCount)
            {
                _availableTokens -= permitCount;
                long remaining = (long)_availableTokens;
                Interlocked.Increment(ref _successCount);
                return RateLimitLease.Acquired(_burstLimit, remaining, _window);
            }

            double deficit = permitCount - _availableTokens;
            TimeSpan retryAfter = TimeSpan.FromMilliseconds(deficit / _tokensPerMillisecond);
            Interlocked.Increment(ref _failCount);
            return RateLimitLease.Rejected(_burstLimit, retryAfter);
        }
    }

    /// <inheritdoc />
    public ValueTask<RateLimitLease> AcquireAsync(int permitCount = 1, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<RateLimitLease>(AttemptAcquire(permitCount));
    }

    /// <inheritdoc />
    public RateLimitStatistics GetStatistics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            RefillTokens();
            return new RateLimitStatistics
            {
                CurrentAvailablePermits = (long)_availableTokens,
                TotalSuccessfulLeases = Interlocked.Read(ref _successCount),
                TotalFailedLeases = Interlocked.Read(ref _failCount),
            };
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private void RefillTokens()
    {
        long now = _timeProvider.GetTimestamp();
        TimeSpan elapsed = _timeProvider.GetElapsedTime(_lastRefillTimestamp, now);
        double tokensToAdd = elapsed.TotalMilliseconds * _tokensPerMillisecond;

        if (tokensToAdd > 0)
        {
            _availableTokens = Math.Min(_burstLimit, _availableTokens + tokensToAdd);
            _lastRefillTimestamp = now;
        }
    }
}
