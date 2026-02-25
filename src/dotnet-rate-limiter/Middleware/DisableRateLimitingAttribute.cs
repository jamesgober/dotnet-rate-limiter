namespace JG.RateLimiter.Middleware;

/// <summary>
/// Disables rate limiting for a controller or action, overriding any default policy.
/// </summary>
/// <example>
/// <code>
/// [DisableRateLimiting]
/// [HttpGet("health")]
/// public IActionResult HealthCheck() => Ok();
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class DisableRateLimitingAttribute : Attribute
{
}
