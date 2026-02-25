using JG.RateLimiter.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace JG.RateLimiter;

/// <summary>
/// Extension methods for registering rate limiting services with dependency injection.
/// </summary>
public static class RateLimitingServiceCollectionExtensions
{
    /// <summary>
    /// Adds rate limiting services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">A delegate to configure <see cref="RateLimitingOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddRateLimiting(options =>
    /// {
    ///     options.AddPolicy("api", new TokenBucketPolicy
    ///     {
    ///         PermitLimit = 100,
    ///         Window = TimeSpan.FromMinutes(1),
    ///         BurstLimit = 20
    ///     });
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddRateLimiting(this IServiceCollection services, Action<RateLimitingOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.TryAddSingleton(TimeProvider.System);

        services.TryAddSingleton<IRateLimitStore>(sp =>
        {
            TimeProvider timeProvider = sp.GetRequiredService<TimeProvider>();
            RateLimitingOptions options = sp.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
            return new InMemoryRateLimitStore(timeProvider, options.IdleTimeout);
        });

        return services;
    }
}
