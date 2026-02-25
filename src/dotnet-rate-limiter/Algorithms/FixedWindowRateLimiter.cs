namespace JG.RateLimiter.Algorithms;

/// <summary>
/// A rate limiter that counts requests in fixed, non-overlapping time windows.
/// When a window expires the counter resets to zero.
/// </summary>
/// <remarks>
/// <para>
/// Simple and predictable, but can allow up to 2× the configured limit at window boundaries
/// when requests cluster at the end of one window and the start of the next.
/// Use <see cref="SlidingWindowRateLimiter"/> if you need smoother enforcement.
/// </para>
/// <para>Thread-safe. All public members may be called concurrently.</para>
/// </remarks>
public sealed class FixedWindowRateLimiter : IRateLimiter
{
    private readonly int _permitLimit;
    private readonly TimeSpan _window;
    private readonly TimeProvider _timeProvider;
    private readonly object _lock = new();

    private long _windowStart;
    private int _requestCount;
    private long _successCount;
    private long _failCount;
    private volatile bool _disposed;

    /// <summary>
    /// Creates a new fixed window rate limiter.
    /// </summary>
    /// <param name="permitLimit">Maximum number of permits allowed per window.</param>
    /// <param name="window">Duration of each fixed time window.</param>
    /// <param name="timeProvider">
    /// Optional time provider for testing. Defaults to <see cref="TimeProvider.System"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="permitLimit"/> is less than 1 or
    /// <paramref name="window"/> is not a positive duration.
    /// </exception>
    public FixedWindowRateLimiter(int permitLimit, TimeSpan window, TimeProvider? timeProvider = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(permitLimit, 1);

        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window), window, "Window must be a positive duration.");
        }

        _permitLimit = permitLimit;
        _window = window;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _windowStart = _timeProvider.GetTimestamp();
    }

    /// <inheritdoc />
    public RateLimitLease AttemptAcquire(int permitCount = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(permitCount, 1);
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            TryResetWindow();

            if ((long)_requestCount + permitCount <= _permitLimit)
            {
                _requestCount += permitCount;
                long remaining = _permitLimit - _requestCount;
                TimeSpan resetAfter = _window - GetWindowElapsed();
                Interlocked.Increment(ref _successCount);
                return RateLimitLease.Acquired(_permitLimit, remaining, resetAfter);
            }

            TimeSpan retryAfter = _window - GetWindowElapsed();
            Interlocked.Increment(ref _failCount);
            return RateLimitLease.Rejected(_permitLimit, retryAfter);
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
            TryResetWindow();
            return new RateLimitStatistics
            {
                CurrentAvailablePermits = _permitLimit - _requestCount,
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

    private void TryResetWindow()
    {
        if (GetWindowElapsed() >= _window)
        {
            _windowStart = _timeProvider.GetTimestamp();
            _requestCount = 0;
        }
    }

    private TimeSpan GetWindowElapsed()
    {
        return _timeProvider.GetElapsedTime(_windowStart, _timeProvider.GetTimestamp());
    }
}
