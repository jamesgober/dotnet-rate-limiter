namespace JG.RateLimiter.Internal;

/// <summary>
/// Standard header names for rate limiting responses.
/// </summary>
internal static class RateLimitHeaders
{
    internal const string Limit = "X-RateLimit-Limit";
    internal const string Remaining = "X-RateLimit-Remaining";
    internal const string Reset = "X-RateLimit-Reset";
    internal const string RetryAfter = "Retry-After";
}
