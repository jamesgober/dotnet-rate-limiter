using JG.RateLimiter.Algorithms;
using Microsoft.AspNetCore.Http;

namespace JG.RateLimiter.Policies;

/// <summary>
/// Configures a fixed window rate limiting policy.
/// </summary>
/// <example>
/// <code>
/// new FixedWindowPolicy
/// {
///     PermitLimit = 1000,
///     Window = TimeSpan.FromHours(1),
///     PartitionBy = ctx => ctx.Request.Headers["X-Api-Key"].ToString()
/// }
/// </code>
/// </example>
public sealed class FixedWindowPolicy : IRateLimiterPolicy
{
    /// <summary>
    /// Gets or sets the maximum number of permits allowed per window. Must be at least 1.
    /// </summary>
    public int PermitLimit { get; set; }

    /// <summary>
    /// Gets or sets the duration of each fixed time window.
    /// </summary>
    public TimeSpan Window { get; set; }

    /// <summary>
    /// Gets or sets the function that extracts a partition key from the request.
    /// Defaults to the client's remote IP address.
    /// </summary>
    public Func<HttpContext, string?> PartitionBy { get; set; } = DefaultPartition;

    /// <inheritdoc />
    IRateLimiter IRateLimiterPolicy.CreateLimiter(TimeProvider timeProvider)
    {
        return new FixedWindowRateLimiter(PermitLimit, Window, timeProvider);
    }

    private static string? DefaultPartition(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString();
}
