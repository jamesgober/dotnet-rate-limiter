using System.Collections.Concurrent;

namespace JG.RateLimiter.Storage;

/// <summary>
/// An in-memory store for <see cref="IRateLimiter"/> instances keyed by policy and partition.
/// Periodically removes idle entries to prevent unbounded memory growth.
/// </summary>
/// <remarks>
/// Thread-safe. Suitable for single-instance deployments. For distributed scenarios,
/// use a Redis-backed store.
/// </remarks>
internal sealed class InMemoryRateLimitStore : IRateLimitStore
{
    private readonly ConcurrentDictionary<string, LimiterEntry> _limiters = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _idleTimeout;
    private readonly Timer? _cleanupTimer;
    private volatile bool _disposed;

    internal InMemoryRateLimitStore(TimeProvider timeProvider, TimeSpan idleTimeout)
    {
        _timeProvider = timeProvider;
        _idleTimeout = idleTimeout;

        TimeSpan cleanupInterval = idleTimeout > TimeSpan.FromMinutes(1)
            ? TimeSpan.FromMinutes(1)
            : idleTimeout;

        _cleanupTimer = new Timer(
            static state => ((InMemoryRateLimitStore)state!).RemoveIdleEntries(),
            this,
            cleanupInterval,
            cleanupInterval);
    }

    /// <inheritdoc />
    public IRateLimiter GetOrCreate(string policyName, string partitionKey, Func<IRateLimiter> factory)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        string key = BuildKey(policyName, partitionKey);
        LimiterEntry entry = _limiters.GetOrAdd(key, static (_, f) => new LimiterEntry(f()), factory);
        entry.LastAccessTimestamp = _timeProvider.GetTimestamp();
        return entry.Limiter;
    }

    /// <inheritdoc />
    public bool TryRemove(string policyName, string partitionKey)
    {
        string key = BuildKey(policyName, partitionKey);

        if (_limiters.TryRemove(key, out LimiterEntry? entry))
        {
            entry.Limiter.Dispose();
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public void Clear()
    {
        foreach (System.Collections.Generic.KeyValuePair<string, LimiterEntry> kvp in _limiters)
        {
            if (_limiters.TryRemove(kvp.Key, out LimiterEntry? entry))
            {
                entry.Limiter.Dispose();
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cleanupTimer?.Dispose();
        Clear();
    }

    private void RemoveIdleEntries()
    {
        if (_disposed)
        {
            return;
        }

        long now = _timeProvider.GetTimestamp();

        foreach (System.Collections.Generic.KeyValuePair<string, LimiterEntry> kvp in _limiters)
        {
            TimeSpan idle = _timeProvider.GetElapsedTime(kvp.Value.LastAccessTimestamp, now);

            if (idle > _idleTimeout)
            {
                if (_limiters.TryRemove(kvp.Key, out LimiterEntry? entry))
                {
                    entry.Limiter.Dispose();
                }
            }
        }
    }

    private static string BuildKey(string policyName, string partitionKey)
    {
        return string.Concat(policyName, ":", partitionKey);
    }

    private sealed class LimiterEntry
    {
        public readonly IRateLimiter Limiter;
        public long LastAccessTimestamp;

        public LimiterEntry(IRateLimiter limiter)
        {
            Limiter = limiter;
        }
    }
}
