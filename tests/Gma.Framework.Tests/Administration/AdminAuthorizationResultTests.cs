namespace Gma.Framework.Tests;

using System.Reflection;
using Gma.Framework.Administration;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdminAuthorizationResultTests
{
    [Fact]
    public void Allowed_has_no_failure_reason()
    {
        AdminAuthorizationResult result = AdminAuthorizationResult.Allowed();

        Assert.True(result.IsAuthorized);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public void Denied_normalizes_failure_reason()
    {
        AdminAuthorizationResult result = AdminAuthorizationResult.Denied(" Not allowed. ");

        Assert.False(result.IsAuthorized);
        Assert.Equal("Not allowed.", result.FailureReason);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Denied_rejects_blank_failure_reason(string reason)
    {
        Assert.Throws<ArgumentException>(() => AdminAuthorizationResult.Denied(reason));
    }

    [Fact]
    public void Denied_rejects_control_characters_in_failure_reason()
    {
        Assert.Throws<ArgumentException>(() => AdminAuthorizationResult.Denied($"No{char.MinValue}pe."));
    }

    [Fact]
    public void Denied_rejects_overlong_failure_reason()
    {
        Assert.Throws<ArgumentException>(() =>
            AdminAuthorizationResult.Denied(new string('x', AdminAuthorizationResult.FailureReasonMaxLength + 1)));
    }

    [Fact]
    public void Result_must_be_created_through_factories()
    {
        ConstructorInfo[] publicConstructors = typeof(AdminAuthorizationResult)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public);

        Assert.Empty(publicConstructors);
    }
}
