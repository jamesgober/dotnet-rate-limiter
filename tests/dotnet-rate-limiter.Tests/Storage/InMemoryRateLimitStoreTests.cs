using FluentAssertions;
using JG.RateLimiter.Algorithms;
using JG.RateLimiter.Storage;
using JG.RateLimiter.Tests.Helpers;

namespace JG.RateLimiter.Tests.Storage;

public sealed class InMemoryRateLimitStoreTests : IDisposable
{
    private readonly FakeTimeProvider _time = new();
    private readonly InMemoryRateLimitStore _store;

    public InMemoryRateLimitStoreTests()
    {
        _store = new InMemoryRateLimitStore(_time, TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void GetOrCreate_NewKey_InvokesFactory()
    {
        bool factoryCalled = false;

        IRateLimiter limiter = _store.GetOrCreate("policy", "key", () =>
        {
            factoryCalled = true;
            return new FixedWindowRateLimiter(10, TimeSpan.FromSeconds(10), _time);
        });

        factoryCalled.Should().BeTrue();
        limiter.Should().NotBeNull();
    }

    [Fact]
    public void GetOrCreate_SameKey_ReturnsSameInstance()
    {
        IRateLimiter first = _store.GetOrCreate("policy", "key",
            () => new FixedWindowRateLimiter(10, TimeSpan.FromSeconds(10), _time));

        IRateLimiter second = _store.GetOrCreate("policy", "key",
            () => new FixedWindowRateLimiter(10, TimeSpan.FromSeconds(10), _time));

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void GetOrCreate_DifferentKeys_ReturnsDifferentInstances()
    {
        IRateLimiter a = _store.GetOrCreate("policy", "keyA",
            () => new FixedWindowRateLimiter(10, TimeSpan.FromSeconds(10), _time));

        IRateLimiter b = _store.GetOrCreate("policy", "keyB",
            () => new FixedWindowRateLimiter(10, TimeSpan.FromSeconds(10), _time));

        b.Should().NotBeSameAs(a);
    }

    [Fact]
    public void TryRemove_ExistingKey_ReturnsTrue()
    {
        _store.GetOrCreate("policy", "key",
            () => new FixedWindowRateLimiter(10, TimeSpan.FromSeconds(10), _time));

        bool removed = _store.TryRemove("policy", "key");

        removed.Should().BeTrue();
    }

    [Fact]
    public void TryRemove_NonexistentKey_ReturnsFalse()
    {
        bool removed = _store.TryRemove("policy", "ghost");

        removed.Should().BeFalse();
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        _store.GetOrCreate("p1", "k1",
            () => new FixedWindowRateLimiter(10, TimeSpan.FromSeconds(10), _time));
        _store.GetOrCreate("p2", "k2",
            () => new FixedWindowRateLimiter(10, TimeSpan.FromSeconds(10), _time));

        _store.Clear();

        // After clear, getting same keys should invoke factory again (new instances).
        bool factoryCalled = false;
        _store.GetOrCreate("p1", "k1", () =>
        {
            factoryCalled = true;
            return new FixedWindowRateLimiter(10, TimeSpan.FromSeconds(10), _time);
        });

        factoryCalled.Should().BeTrue();
    }

    [Fact]
    public void Dispose_PreventsSubsequentGetOrCreate()
    {
        _store.Dispose();

        Action act = () => _store.GetOrCreate("policy", "key",
            () => new FixedWindowRateLimiter(10, TimeSpan.FromSeconds(10), _time));

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void IdleCleanup_RemovesExpiredEntries()
    {
        // Use a short idle timeout so the cleanup timer fires quickly.
        using var shortStore = new InMemoryRateLimitStore(_time, TimeSpan.FromSeconds(2));

        IRateLimiter limiter = shortStore.GetOrCreate("policy", "key",
            () => new FixedWindowRateLimiter(10, TimeSpan.FromSeconds(10), _time));
        limiter.Should().NotBeNull();

        // Advance time past the idle timeout.
        _time.Advance(TimeSpan.FromSeconds(5));

        // Wait briefly for the timer to fire (Timer uses real time, not FakeTimeProvider).
        Thread.Sleep(3000);

        // The entry should have been evicted. A new factory call means a new instance.
        bool factoryCalled = false;
        shortStore.GetOrCreate("policy", "key", () =>
        {
            factoryCalled = true;
            return new FixedWindowRateLimiter(10, TimeSpan.FromSeconds(10), _time);
        });

        factoryCalled.Should().BeTrue();
    }

    public void Dispose()
    {
        _store.Dispose();
    }
}
