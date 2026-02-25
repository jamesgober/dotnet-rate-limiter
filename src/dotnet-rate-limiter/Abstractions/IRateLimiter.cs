namespace JG.RateLimiter;

/// <summary>
/// Defines the contract for a rate limiter that controls the rate of operations.
/// </summary>
/// <remarks>
/// Implementations must be thread-safe. A single <see cref="IRateLimiter"/> instance
/// may be accessed concurrently from multiple threads.
/// </remarks>
public interface IRateLimiter : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Attempts to acquire the specified number of permits synchronously without waiting.
    /// </summary>
    /// <param name="permitCount">The number of permits to acquire. Must be at least 1.</param>
    /// <returns>A <see cref="RateLimitLease"/> indicating whether the permits were granted.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="permitCount"/> is less than 1.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the limiter has been disposed.</exception>
    RateLimitLease AttemptAcquire(int permitCount = 1);

    /// <summary>
    /// Asynchronously acquires the specified number of permits, potentially waiting if necessary.
    /// </summary>
    /// <param name="permitCount">The number of permits to acquire. Must be at least 1.</param>
    /// <param name="cancellationToken">A token to cancel the wait.</param>
    /// <returns>A <see cref="RateLimitLease"/> indicating whether the permits were granted.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="permitCount"/> is less than 1.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the limiter has been disposed.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    ValueTask<RateLimitLease> AcquireAsync(int permitCount = 1, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a snapshot of the current rate limiter statistics.
    /// </summary>
    /// <returns>Current counters for this rate limiter instance.</returns>
    RateLimitStatistics GetStatistics();
}
