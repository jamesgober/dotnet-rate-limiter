using FluentAssertions;
using JG.RateLimiter.Middleware;

namespace JG.RateLimiter.Tests.Middleware;

public sealed class RateLimitAttributeTests
{
    [Fact]
    public void Constructor_SetsPolicyName()
    {
        var attribute = new RateLimitAttribute("my-policy");

        attribute.PolicyName.Should().Be("my-policy");
    }

    [Fact]
    public void Constructor_NullPolicyName_Throws()
    {
        Action act = () => new RateLimitAttribute(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Attribute_IsNotInheritable_MultipleNotAllowed()
    {
        var usage = typeof(RateLimitAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        usage.AllowMultiple.Should().BeFalse();
        usage.Inherited.Should().BeTrue();
        usage.ValidOn.Should().HaveFlag(AttributeTargets.Class);
        usage.ValidOn.Should().HaveFlag(AttributeTargets.Method);
    }
}

public sealed class DisableRateLimitingAttributeTests
{
    [Fact]
    public void Attribute_CanBeInstantiated()
    {
        var attribute = new DisableRateLimitingAttribute();

        attribute.Should().NotBeNull();
    }

    [Fact]
    public void Attribute_HasCorrectUsage()
    {
        var usage = typeof(DisableRateLimitingAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        usage.AllowMultiple.Should().BeFalse();
        usage.Inherited.Should().BeTrue();
        usage.ValidOn.Should().HaveFlag(AttributeTargets.Class);
        usage.ValidOn.Should().HaveFlag(AttributeTargets.Method);
    }
}
