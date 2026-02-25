namespace JG.RateLimiter.Middleware;

/// <summary>
/// Specifies the rate limiting policy to apply to a controller or action.
/// </summary>
/// <example>
/// <code>
/// [RateLimit("api")]
/// public IActionResult Get() => Ok();
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RateLimitAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the rate limiting policy to apply.
    /// Must match a policy registered in <see cref="RateLimitingOptions.AddPolicy"/>.
    /// </summary>
    public string PolicyName { get; }

    /// <summary>
    /// Creates a new <see cref="RateLimitAttribute"/> targeting the named policy.
    /// </summary>
    /// <param name="policyName">The name of the registered rate limiting policy.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="policyName"/> is <c>null</c>.</exception>
    public RateLimitAttribute(string policyName)
    {
        ArgumentNullException.ThrowIfNull(policyName);
        PolicyName = policyName;
    }
}
