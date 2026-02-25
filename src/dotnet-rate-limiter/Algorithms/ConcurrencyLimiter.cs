namespace JG.RateLimiter.Algorithms;

/// <summary>
/// A rate limiter that caps the number of concurrent in-flight operations.
/// Permits are returned when the <see cref="RateLimitLease"/> is disposed.
/// </summary>
/// <remarks>
/// <para>
/// Unlike time-window algorithms, this limiter does not track request rates.
/// Instead, it ensures that no more than <c>PermitLimit</c> operations execute
/// simultaneously. Callers <b>must</b> dispose the returned lease to release permits.
/// </para>
/// <para>Thread-safe. All public members may be called concurrently.</para>
/// </remarks>
public sealed class ConcurrencyLimiter : IRateLimiter
{
    private readonly int _permitLimit;
    private readonly object _lock = new();

    private int _activeCount;
    private long _successCount;
    private long _failCount;
    private volatile bool _disposed;

    /// <summary>
    /// Creates a new concurrency limiter.
    /// </summary>
    /// <param name="permitLimit">Maximum number of concurrent permits. Must be at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="permitLimit"/> is less than 1.
    /// </exception>
    public ConcurrencyLimiter(int permitLimit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(permitLimit, 1);
        _permitLimit = permitLimit;
    }

    /// <inheritdoc />
    public RateLimitLease AttemptAcquire(int permitCount = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(permitCount, 1);
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            if ((long)_activeCount + permitCount <= _permitLimit)
            {
                _activeCount += permitCount;
                int captured = permitCount;
                long remaining = _permitLimit - _activeCount;
                Interlocked.Increment(ref _successCount);
                return RateLimitLease.Acquired(_permitLimit, remaining, resetAfter: null, onDispose: () => Release(captured));
            }
        }

        Interlocked.Increment(ref _failCount);
        return RateLimitLease.Rejected(_permitLimit, retryAfter: null);
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

        return new RateLimitStatistics
        {
            CurrentAvailablePermits = _permitLimit - Volatile.Read(ref _activeCount),
            TotalSuccessfulLeases = Interlocked.Read(ref _successCount),
            TotalFailedLeases = Interlocked.Read(ref _failCount),
        };
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

    private void Release(int permitCount)
    {
        lock (_lock)
        {
            _activeCount = Math.Max(0, _activeCount - permitCount);
        }
    }
}
