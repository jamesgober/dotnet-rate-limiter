using JG.RateLimiter.Middleware;
using Microsoft.AspNetCore.Builder;

namespace JG.RateLimiter;

/// <summary>
/// Extension methods for adding rate limiting middleware to the ASP.NET Core pipeline.
/// </summary>
public static class RateLimitingApplicationBuilderExtensions
{
    /// <summary>
    /// Adds rate limiting middleware to the request pipeline. Must be called after
    /// <c>UseRouting</c> so endpoint metadata (e.g., <see cref="RateLimitAttribute"/>)
    /// is available.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The <see cref="IApplicationBuilder"/> for chaining.</returns>
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<RateLimitingMiddleware>();
    }
}
