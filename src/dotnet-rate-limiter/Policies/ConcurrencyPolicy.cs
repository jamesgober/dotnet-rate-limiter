using JG.RateLimiter.Algorithms;
using Microsoft.AspNetCore.Http;

namespace JG.RateLimiter.Policies;

/// <summary>
/// Configures a concurrency limiting policy that caps simultaneous in-flight requests.
/// </summary>
/// <example>
/// <code>
/// new ConcurrencyPolicy
/// {
///     PermitLimit = 10,
///     PartitionBy = ctx => ctx.Connection.RemoteIpAddress?.ToString()
/// }
/// </code>
/// </example>
public sealed class ConcurrencyPolicy : IRateLimiterPolicy
{
    /// <summary>
    /// Gets or sets the maximum number of concurrent permits. Must be at least 1.
    /// </summary>
    public int PermitLimit { get; set; }

    /// <summary>
    /// Gets or sets the function that extracts a partition key from the request.
    /// Defaults to the client's remote IP address.
    /// </summary>
    public Func<HttpContext, string?> PartitionBy { get; set; } = DefaultPartition;

    /// <inheritdoc />
    IRateLimiter IRateLimiterPolicy.CreateLimiter(TimeProvider timeProvider)
    {
        return new ConcurrencyLimiter(PermitLimit);
    }

    private static string? DefaultPartition(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString();
}
