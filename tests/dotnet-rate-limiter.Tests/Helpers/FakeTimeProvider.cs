namespace JG.RateLimiter.Tests.Helpers;

/// <summary>
/// A time provider that allows manual advancement of time for deterministic tests.
/// </summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private long _currentTicks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => Volatile.Read(ref _currentTicks);

    /// <summary>
    /// Advances the clock by the specified duration.
    /// </summary>
    public void Advance(TimeSpan duration)
    {
        Interlocked.Add(ref _currentTicks, duration.Ticks);
    }
}
