namespace JG.RateLimiter;

/// <summary>
/// Represents the result of attempting to acquire permits from a rate limiter.
/// </summary>
/// <remarks>
/// Leases that carry a release callback (e.g., from a <see cref="Algorithms.ConcurrencyLimiter"/>)
/// must be disposed to return the permits. Always use a <c>using</c> statement or block.
/// </remarks>
public sealed class RateLimitLease : IDisposable
{
    private Action? _onDispose;
    private int _disposed;

    private RateLimitLease(bool isAcquired, long limit, long remaining, TimeSpan? retryAfter, TimeSpan? resetAfter, Action? onDispose)
    {
        IsAcquired = isAcquired;
        Limit = limit;
        Remaining = remaining;
        RetryAfter = retryAfter;
        ResetAfter = resetAfter;
        _onDispose = onDispose;
    }

    /// <summary>
    /// Gets a value indicating whether the requested permits were successfully acquired.
    /// </summary>
    public bool IsAcquired { get; }

    /// <summary>
    /// Gets the maximum number of permits allowed by the policy (the configured limit).
    /// </summary>
    public long Limit { get; }

    /// <summary>
    /// Gets the number of permits still available after this acquisition.
    /// </summary>
    public long Remaining { get; }

    /// <summary>
    /// Gets the suggested time to wait before retrying, or <c>null</c> if not applicable.
    /// Populated when <see cref="IsAcquired"/> is <c>false</c>.
    /// </summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>
    /// Gets the time until the current rate limit window resets, or <c>null</c> if not applicable.
    /// </summary>
    public TimeSpan? ResetAfter { get; }

    /// <summary>
    /// Creates a lease representing a successful acquisition.
    /// </summary>
    internal static RateLimitLease Acquired(long limit, long remaining, TimeSpan? resetAfter, Action? onDispose = null)
        => new(true, limit, remaining, retryAfter: null, resetAfter, onDispose);

    /// <summary>
    /// Creates a lease representing a rejected acquisition.
    /// </summary>
    internal static RateLimitLease Rejected(long limit, TimeSpan? retryAfter)
        => new(false, limit, 0, retryAfter, resetAfter: null, onDispose: null);

    /// <summary>
    /// Releases any held permits back to the rate limiter. Safe to call multiple times.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _onDispose?.Invoke();
        _onDispose = null;
    }
}
