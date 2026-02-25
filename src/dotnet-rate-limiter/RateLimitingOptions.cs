using Microsoft.AspNetCore.Http;

namespace JG.RateLimiter;

/// <summary>
/// Configuration for the rate limiting middleware. Holds registered policies
/// and controls rejection behavior.
/// </summary>
public sealed class RateLimitingOptions
{
    private readonly Dictionary<string, IRateLimiterPolicy> _policies = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the name of the default policy applied to endpoints without a
    /// <see cref="Middleware.RateLimitAttribute"/>. Endpoints decorated with
    /// <see cref="Middleware.DisableRateLimitingAttribute"/> are always excluded.
    /// If <c>null</c>, unattributed endpoints are not rate-limited.
    /// </summary>
    public string? DefaultPolicyName { get; set; }

    /// <summary>
    /// Gets or sets the HTTP status code returned when a request is rejected.
    /// Defaults to <c>429</c> (Too Many Requests).
    /// </summary>
    public int RejectionStatusCode { get; set; } = 429;

    /// <summary>
    /// Gets or sets an optional callback invoked when a request is rejected.
    /// Use this to customize the response body or headers. When set, the middleware
    /// will not write a status code automatically — the callback is responsible for
    /// setting the response.
    /// </summary>
    public Func<HttpContext, RateLimitLease, CancellationToken, ValueTask>? OnRejected { get; set; }

    /// <summary>
    /// Gets or sets how long a rate limiter can sit idle before it is removed from the store.
    /// Defaults to 10 minutes.
    /// </summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Registers a rate limiting policy under the given name.
    /// </summary>
    /// <param name="policyName">A unique name for the policy. Used in <see cref="Middleware.RateLimitAttribute"/>.</param>
    /// <param name="policy">The policy configuration.</param>
    /// <returns>This <see cref="RateLimitingOptions"/> instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="policyName"/> or <paramref name="policy"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="policyName"/> is empty or whitespace.</exception>
    public RateLimitingOptions AddPolicy(string policyName, IRateLimiterPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policyName);
        ArgumentNullException.ThrowIfNull(policy);

        if (string.IsNullOrWhiteSpace(policyName))
        {
            throw new ArgumentException("Policy name cannot be empty or whitespace.", nameof(policyName));
        }

        _policies[policyName] = policy;
        return this;
    }

    /// <summary>
    /// Attempts to retrieve a registered policy by name.
    /// </summary>
    internal bool TryGetPolicy(string name, out IRateLimiterPolicy policy)
    {
        return _policies.TryGetValue(name, out policy!);
    }

    /// <summary>
    /// Returns all registered policy names. Used internally for diagnostics.
    /// </summary>
    internal IReadOnlyCollection<string> PolicyNames => _policies.Keys;
}
