using System.Globalization;
using JG.RateLimiter.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JG.RateLimiter.Middleware;

/// <summary>
/// ASP.NET Core middleware that enforces rate limiting policies on incoming requests.
/// </summary>
internal sealed class RateLimitingMiddleware
{
    private const string GlobalPartitionKey = "__global";

    private readonly RequestDelegate _next;
    private readonly RateLimitingOptions _options;
    private readonly IRateLimitStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RateLimitingMiddleware> _logger;

    public RateLimitingMiddleware(
        RequestDelegate next,
        IOptions<RateLimitingOptions> options,
        IRateLimitStore store,
        TimeProvider timeProvider,
        ILogger<RateLimitingMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _next = next;
        _options = options.Value;
        _store = store;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        Endpoint? endpoint = context.GetEndpoint();

        // [DisableRateLimiting] opts out entirely.
        if (endpoint?.Metadata.GetMetadata<DisableRateLimitingAttribute>() is not null)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        string? policyName = ResolvePolicyName(endpoint);

        if (policyName is null || !_options.TryGetPolicy(policyName, out IRateLimiterPolicy? policy))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        string partitionKey = ResolvePartitionKey(policy, context);
        IRateLimiter limiter = _store.GetOrCreate(policyName, partitionKey, CreateFactory(policy));

        using RateLimitLease lease = limiter.AttemptAcquire();

        WriteRateLimitHeaders(context.Response, lease);

        if (lease.IsAcquired)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        await HandleRejectionAsync(context, lease).ConfigureAwait(false);
    }

    private string? ResolvePolicyName(Endpoint? endpoint)
    {
        RateLimitAttribute? attribute = endpoint?.Metadata.GetMetadata<RateLimitAttribute>();
        return attribute?.PolicyName ?? _options.DefaultPolicyName;
    }

    private string ResolvePartitionKey(IRateLimiterPolicy policy, HttpContext context)
    {
        try
        {
            string? key = policy.PartitionBy(context);
            return string.IsNullOrEmpty(key) ? GlobalPartitionKey : key;
        }
        catch (Exception ex)
        {
            // Graceful degradation: if the partition resolver fails, fall back to global.
            _logger.LogWarning(ex, "PartitionBy delegate threw an exception. Falling back to global partition.");
            return GlobalPartitionKey;
        }
    }

    /// <summary>
    /// Returns a factory delegate that captures the policy in a lightweight manner.
    /// The TimeProvider is accessed from the field to avoid a second capture.
    /// </summary>
    private Func<IRateLimiter> CreateFactory(IRateLimiterPolicy policy)
    {
        TimeProvider tp = _timeProvider;
        return () => policy.CreateLimiter(tp);
    }

    private static void WriteRateLimitHeaders(HttpResponse response, RateLimitLease lease)
    {
        if (response.HasStarted)
        {
            return;
        }

        response.Headers[RateLimitHeaders.Limit] = lease.Limit.ToString(CultureInfo.InvariantCulture);
        response.Headers[RateLimitHeaders.Remaining] = lease.Remaining.ToString(CultureInfo.InvariantCulture);

        if (lease.ResetAfter.HasValue)
        {
            int resetSeconds = (int)Math.Ceiling(lease.ResetAfter.Value.TotalSeconds);
            response.Headers[RateLimitHeaders.Reset] = resetSeconds.ToString(CultureInfo.InvariantCulture);
        }

        if (!lease.IsAcquired && lease.RetryAfter.HasValue)
        {
            int retrySeconds = (int)Math.Ceiling(lease.RetryAfter.Value.TotalSeconds);
            response.Headers[RateLimitHeaders.RetryAfter] = retrySeconds.ToString(CultureInfo.InvariantCulture);
        }
    }

    private async ValueTask HandleRejectionAsync(HttpContext context, RateLimitLease lease)
    {
        if (_options.OnRejected is not null)
        {
            try
            {
                await _options.OnRejected(context, lease, context.RequestAborted).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OnRejected callback threw an exception.");
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = _options.RejectionStatusCode;
                }
            }

            return;
        }

        context.Response.StatusCode = _options.RejectionStatusCode;
    }
}
