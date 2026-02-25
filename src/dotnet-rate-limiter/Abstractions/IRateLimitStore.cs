namespace JG.RateLimiter;

/// <summary>
/// Manages <see cref="IRateLimiter"/> instances keyed by policy name and partition key.
/// </summary>
/// <remarks>
/// Implementations must be thread-safe for concurrent access from multiple requests.
/// </remarks>
public interface IRateLimitStore : IDisposable
{
    /// <summary>
    /// Returns an existing rate limiter for the given key, or creates one using the factory.
    /// </summary>
    /// <param name="policyName">The name of the rate limiting policy.</param>
    /// <param name="partitionKey">The partition key (e.g., client IP, API key).</param>
    /// <param name="factory">A factory delegate invoked when no limiter exists for this key.</param>
    /// <returns>The rate limiter associated with the composite key.</returns>
    IRateLimiter GetOrCreate(string policyName, string partitionKey, Func<IRateLimiter> factory);

    /// <summary>
    /// Removes and disposes the rate limiter for the given key if it exists.
    /// </summary>
    /// <param name="policyName">The policy name.</param>
    /// <param name="partitionKey">The partition key.</param>
    /// <returns><c>true</c> if a limiter was found and removed; otherwise <c>false</c>.</returns>
    bool TryRemove(string policyName, string partitionKey);

    /// <summary>
    /// Removes and disposes all stored rate limiter instances.
    /// </summary>
    void Clear();
}
