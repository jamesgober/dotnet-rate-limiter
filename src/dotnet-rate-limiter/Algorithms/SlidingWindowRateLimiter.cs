namespace JG.RateLimiter.Algorithms;

/// <summary>
/// A rate limiter that counts requests over a rolling time window divided into segments.
/// Older segments are discarded as time advances, providing smoother enforcement than
/// <see cref="FixedWindowRateLimiter"/>.
/// </summary>
/// <remarks>
/// <para>
/// The window is split into <c>SegmentsPerWindow</c> equal sub-intervals.
/// The total request count across all active segments determines whether new
/// requests are allowed. As time progresses, expired segments are zeroed out.
/// </para>
/// <para>Thread-safe. All public members may be called concurrently.</para>
/// </remarks>
public sealed class SlidingWindowRateLimiter : IRateLimiter
{
    private readonly int _permitLimit;
    private readonly TimeSpan _window;
    private readonly int _segmentsPerWindow;
    private readonly TimeSpan _segmentDuration;
    private readonly TimeProvider _timeProvider;
    private readonly object _lock = new();

    private readonly int[] _segmentCounts;
    private int _currentSegmentIndex;
    private long _currentSegmentStart;
    private long _successCount;
    private long _failCount;
    private volatile bool _disposed;

    /// <summary>
    /// Creates a new sliding window rate limiter.
    /// </summary>
    /// <param name="permitLimit">Maximum number of permits allowed within the rolling window.</param>
    /// <param name="window">Total duration of the sliding window.</param>
    /// <param name="segmentsPerWindow">
    /// Number of sub-segments the window is divided into. Higher values give finer granularity
    /// at the cost of slightly more bookkeeping. Must be at least 2.
    /// </param>
    /// <param name="timeProvider">
    /// Optional time provider for testing. Defaults to <see cref="TimeProvider.System"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="permitLimit"/> is less than 1,
    /// <paramref name="segmentsPerWindow"/> is less than 2,
    /// or <paramref name="window"/> is not a positive duration.
    /// </exception>
    public SlidingWindowRateLimiter(int permitLimit, TimeSpan window, int segmentsPerWindow, TimeProvider? timeProvider = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(permitLimit, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(segmentsPerWindow, 2);

        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window), window, "Window must be a positive duration.");
        }

        _permitLimit = permitLimit;
        _window = window;
        _segmentsPerWindow = segmentsPerWindow;
        _segmentDuration = window / segmentsPerWindow;
        _timeProvider = timeProvider ?? TimeProvider.System;

        _segmentCounts = new int[segmentsPerWindow];
        _currentSegmentIndex = 0;
        _currentSegmentStart = _timeProvider.GetTimestamp();
    }

    /// <inheritdoc />
    public RateLimitLease AttemptAcquire(int permitCount = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(permitCount, 1);
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            AdvanceSegments();

            int totalCount = SumSegments();

            if ((long)totalCount + permitCount <= _permitLimit)
            {
                _segmentCounts[_currentSegmentIndex] += permitCount;
                long remaining = _permitLimit - totalCount - permitCount;
                Interlocked.Increment(ref _successCount);
                return RateLimitLease.Acquired(_permitLimit, remaining, CalculateResetAfter());
            }

            Interlocked.Increment(ref _failCount);
            return RateLimitLease.Rejected(_permitLimit, CalculateRetryAfter());
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
            AdvanceSegments();
            int totalCount = SumSegments();
            return new RateLimitStatistics
            {
                CurrentAvailablePermits = _permitLimit - totalCount,
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

    private void AdvanceSegments()
    {
        long now = _timeProvider.GetTimestamp();
        TimeSpan elapsed = _timeProvider.GetElapsedTime(_currentSegmentStart, now);
        int segmentsToAdvance = (int)(elapsed.Ticks / _segmentDuration.Ticks);

        if (segmentsToAdvance <= 0)
        {
            return;
        }

        // If more segments have elapsed than exist, just clear everything.
        int clearCount = Math.Min(segmentsToAdvance, _segmentsPerWindow);

        for (int i = 0; i < clearCount; i++)
        {
            _currentSegmentIndex = (_currentSegmentIndex + 1) % _segmentsPerWindow;
            _segmentCounts[_currentSegmentIndex] = 0;
        }

        // Snap the start forward by the number of segments advanced to prevent drift.
        _currentSegmentStart = now;
    }

    private int SumSegments()
    {
        int total = 0;
        for (int i = 0; i < _segmentsPerWindow; i++)
        {
            total += _segmentCounts[i];
        }
        return total;
    }

    private TimeSpan CalculateResetAfter()
    {
        TimeSpan elapsed = _timeProvider.GetElapsedTime(_currentSegmentStart, _timeProvider.GetTimestamp());
        TimeSpan remaining = _segmentDuration - elapsed;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private TimeSpan CalculateRetryAfter()
    {
        // The oldest segment will expire after one segment duration from now.
        return CalculateResetAfter();
    }
}
