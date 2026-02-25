using Microsoft.AspNetCore.Http;

namespace JG.RateLimiter;

/// <summary>
/// Defines a rate limiting policy that resolves partition keys and creates
/// <see cref="IRateLimiter"/> instances for each partition.
/// </summary>
public interface IRateLimiterPolicy
{
    /// <summary>
    /// A delegate that extracts a partition key from the incoming HTTP request.
    /// Requests with the same partition key share a rate limiter instance.
    /// </summary>
    /// <remarks>
    /// If the delegate returns <c>null</c>, the request is assigned to a global partition.
    /// </remarks>
    Func<HttpContext, string?> PartitionBy { get; }

    /// <summary>
    /// Creates a new <see cref="IRateLimiter"/> instance for a partition.
    /// Called once per unique partition key; the result is cached by the store.
    /// </summary>
    /// <param name="timeProvider">The time provider used by time-based algorithms.</param>
    /// <returns>A new rate limiter configured according to this policy.</returns>
    IRateLimiter CreateLimiter(TimeProvider timeProvider);
}
