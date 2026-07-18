namespace Gma.Framework.Tests.Security;

using Gma.Framework.Security;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AuthenticationAssuranceRequirementTests
{
    [Fact]
    public void Normalizes_contexts_without_ranking_or_case_folding()
    {
        AuthenticationAssuranceRequirement requirement = new(
            [" urn:test:acr:password ", "urn:test:acr:password", "URN:test:acr:password"],
            TimeSpan.FromMinutes(10));

        Assert.Equal(
            ["urn:test:acr:password", "URN:test:acr:password"],
            requirement.AcceptedContextReferences);
        Assert.Equal(TimeSpan.FromMinutes(10), requirement.MaxAuthenticationAge);
    }

    [Fact]
    public void Requires_context_or_freshness()
    {
        Assert.Throws<ArgumentException>(() => new AuthenticationAssuranceRequirement());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("urn:test:acr:with space")]
    [InlineData("urn:test:acr:with\tcontrol")]
    public void Rejects_invalid_context_reference(string value)
    {
        Assert.Throws<ArgumentException>(() => new AuthenticationAssuranceRequirement([value]));
    }

    [Fact]
    public void Rejects_non_positive_maximum_age()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AuthenticationAssuranceRequirement(maxAuthenticationAge: TimeSpan.Zero));
    }
}
