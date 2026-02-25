using JG.RateLimiter.Algorithms;
using Microsoft.AspNetCore.Http;

namespace JG.RateLimiter.Policies;

/// <summary>
/// Configures a token bucket rate limiting policy.
/// </summary>
/// <example>
/// <code>
/// new TokenBucketPolicy
/// {
///     PermitLimit = 100,
///     Window = TimeSpan.FromMinutes(1),
///     BurstLimit = 20,
///     PartitionBy = ctx => ctx.Connection.RemoteIpAddress?.ToString()
/// }
/// </code>
/// </example>
public sealed class TokenBucketPolicy : IRateLimiterPolicy
{
    /// <summary>
    /// Gets or sets the number of permits replenished per <see cref="Window"/>.
    /// Must be at least 1.
    /// </summary>
    public int PermitLimit { get; set; }

    /// <summary>
    /// Gets or sets the time period over which <see cref="PermitLimit"/> tokens are added.
    /// </summary>
    public TimeSpan Window { get; set; }

    /// <summary>
    /// Gets or sets the maximum token capacity (burst size).
    /// Requests beyond this are rejected even after a long idle period. Must be at least 1.
    /// </summary>
    public int BurstLimit { get; set; }

    /// <summary>
    /// Gets or sets the function that extracts a partition key from the request.
    /// Defaults to the client's remote IP address.
    /// </summary>
    public Func<HttpContext, string?> PartitionBy { get; set; } = DefaultPartition;

    /// <inheritdoc />
    IRateLimiter IRateLimiterPolicy.CreateLimiter(TimeProvider timeProvider)
    {
        return new TokenBucketRateLimiter(PermitLimit, Window, BurstLimit, timeProvider);
    }

    private static string? DefaultPartition(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString();
}
