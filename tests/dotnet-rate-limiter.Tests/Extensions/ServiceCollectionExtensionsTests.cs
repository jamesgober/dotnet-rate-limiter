using FluentAssertions;
using JG.RateLimiter.Policies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JG.RateLimiter.Tests.Extensions;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddRateLimiting_RegistersOptions()
    {
        var services = new ServiceCollection();

        services.AddRateLimiting(options =>
        {
            options.AddPolicy("api", new TokenBucketPolicy
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                BurstLimit = 20,
            });
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        RateLimitingOptions options = provider.GetRequiredService<IOptions<RateLimitingOptions>>().Value;

        options.TryGetPolicy("api", out IRateLimiterPolicy? policy).Should().BeTrue();
        policy.Should().BeOfType<TokenBucketPolicy>();
    }

    [Fact]
    public void AddRateLimiting_RegistersStore()
    {
        var services = new ServiceCollection();

        services.AddRateLimiting(options =>
        {
            options.AddPolicy("test", new FixedWindowPolicy
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            });
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        IRateLimitStore store = provider.GetRequiredService<IRateLimitStore>();

        store.Should().NotBeNull();
    }

    [Fact]
    public void AddRateLimiting_RegistersTimeProvider()
    {
        var services = new ServiceCollection();

        services.AddRateLimiting(_ => { });

        using ServiceProvider provider = services.BuildServiceProvider();
        TimeProvider tp = provider.GetRequiredService<TimeProvider>();

        tp.Should().Be(TimeProvider.System);
    }

    [Fact]
    public void AddRateLimiting_NullServices_Throws()
    {
        IServiceCollection services = null!;

        Action act = () => services.AddRateLimiting(_ => { });

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddRateLimiting_NullConfigure_Throws()
    {
        var services = new ServiceCollection();

        Action act = () => services.AddRateLimiting(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddRateLimiting_CustomTimeProvider_IsRespected()
    {
        var customTime = new Helpers.FakeTimeProvider();
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(customTime);

        services.AddRateLimiting(_ => { });

        using ServiceProvider provider = services.BuildServiceProvider();
        TimeProvider tp = provider.GetRequiredService<TimeProvider>();

        tp.Should().BeSameAs(customTime);
    }
}
