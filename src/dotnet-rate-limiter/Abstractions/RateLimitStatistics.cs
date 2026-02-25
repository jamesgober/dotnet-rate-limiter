namespace JG.RateLimiter;

/// <summary>
/// A snapshot of counters for a rate limiter instance.
/// </summary>
public readonly record struct RateLimitStatistics
{
    /// <summary>
    /// Gets the number of permits currently available for acquisition.
    /// </summary>
    public long CurrentAvailablePermits { get; init; }

    /// <summary>
    /// Gets the total number of successful lease acquisitions since creation.
    /// </summary>
    public long TotalSuccessfulLeases { get; init; }

    /// <summary>
    /// Gets the total number of failed (rejected) lease acquisitions since creation.
    /// </summary>
    public long TotalFailedLeases { get; init; }
}
