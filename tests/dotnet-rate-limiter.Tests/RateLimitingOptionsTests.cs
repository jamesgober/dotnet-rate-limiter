using FluentAssertions;
using JG.RateLimiter.Policies;

namespace JG.RateLimiter.Tests;

public sealed class RateLimitingOptionsTests
{
    [Fact]
    public void AddPolicy_RegistersPolicy()
    {
        var options = new RateLimitingOptions();
        var policy = new FixedWindowPolicy
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1)
        };

        options.AddPolicy("test", policy);

        options.TryGetPolicy("test", out IRateLimiterPolicy? resolved).Should().BeTrue();
        resolved.Should().BeSameAs(policy);
    }

    [Fact]
    public void AddPolicy_NullName_Throws()
    {
        var options = new RateLimitingOptions();
        var policy = new FixedWindowPolicy { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) };

        Action act = () => options.AddPolicy(null!, policy);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddPolicy_NullPolicy_Throws()
    {
        var options = new RateLimitingOptions();

        Action act = () => options.AddPolicy("test", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddPolicy_EmptyName_Throws()
    {
        var options = new RateLimitingOptions();
        var policy = new FixedWindowPolicy { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) };

        Action act = () => options.AddPolicy("  ", policy);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddPolicy_SameNameTwice_OverwritesPrevious()
    {
        var options = new RateLimitingOptions();
        var first = new FixedWindowPolicy { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) };
        var second = new FixedWindowPolicy { PermitLimit = 50, Window = TimeSpan.FromMinutes(1) };

        options.AddPolicy("test", first);
        options.AddPolicy("test", second);

        options.TryGetPolicy("test", out IRateLimiterPolicy? resolved).Should().BeTrue();
        resolved.Should().BeSameAs(second);
    }

    [Fact]
    public void TryGetPolicy_NotRegistered_ReturnsFalse()
    {
        var options = new RateLimitingOptions();

        options.TryGetPolicy("missing", out _).Should().BeFalse();
    }

    [Fact]
    public void Defaults_AreCorrect()
    {
        var options = new RateLimitingOptions();

        options.RejectionStatusCode.Should().Be(429);
        options.DefaultPolicyName.Should().BeNull();
        options.OnRejected.Should().BeNull();
        options.IdleTimeout.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void AddPolicy_ReturnsSameInstance_ForChaining()
    {
        var options = new RateLimitingOptions();
        var policy = new FixedWindowPolicy { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) };

        RateLimitingOptions result = options.AddPolicy("test", policy);

        result.Should().BeSameAs(options);
    }
}
