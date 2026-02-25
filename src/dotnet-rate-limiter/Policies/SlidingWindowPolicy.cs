using JG.RateLimiter.Algorithms;
using Microsoft.AspNetCore.Http;

namespace JG.RateLimiter.Policies;

/// <summary>
/// Configures a sliding window rate limiting policy.
/// </summary>
/// <example>
/// <code>
/// new SlidingWindowPolicy
/// {
///     PermitLimit = 100,
///     Window = TimeSpan.FromMinutes(1),
///     SegmentsPerWindow = 6,
///     PartitionBy = ctx => ctx.Connection.RemoteIpAddress?.ToString()
/// }
/// </code>
/// </example>
public sealed class SlidingWindowPolicy : IRateLimiterPolicy
{
    /// <summary>
    /// Gets or sets the maximum number of permits allowed within the rolling window.
    /// Must be at least 1.
    /// </summary>
    public int PermitLimit { get; set; }

    /// <summary>
    /// Gets or sets the total duration of the sliding window.
    /// </summary>
    public TimeSpan Window { get; set; }

    /// <summary>
    /// Gets or sets the number of segments the window is divided into.
    /// Higher values give finer granularity. Must be at least 2. Defaults to 3.
    /// </summary>
    public int SegmentsPerWindow { get; set; } = 3;

    /// <summary>
    /// Gets or sets the function that extracts a partition key from the request.
    /// Defaults to the client's remote IP address.
    /// </summary>
    public Func<HttpContext, string?> PartitionBy { get; set; } = DefaultPartition;

    /// <inheritdoc />
    IRateLimiter IRateLimiterPolicy.CreateLimiter(TimeProvider timeProvider)
    {
        return new SlidingWindowRateLimiter(PermitLimit, Window, SegmentsPerWindow, timeProvider);
    }

    private static string? DefaultPartition(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString();
}
