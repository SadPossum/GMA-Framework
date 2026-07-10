namespace Gma.Framework.Tests;

using Gma.Framework.Security;
using Xunit;

[Trait("Category", "Unit")]
public sealed class GmaClaimNamesTests
{
    [Fact]
    public void Claim_names_are_stable_and_distinct()
    {
        Assert.Equal(256, GmaClaimNames.MaxLength);
        Assert.Equal("sub", GmaClaimNames.Subject);
        Assert.Equal("scope_id", GmaClaimNames.ScopeId);
        Assert.Equal("sid", GmaClaimNames.SessionId);

        Assert.Equal(3, new HashSet<string>(
            [
                GmaClaimNames.Subject,
                GmaClaimNames.ScopeId,
                GmaClaimNames.SessionId
            ],
            StringComparer.Ordinal).Count);
    }

    [Theory]
    [InlineData("sub", true)]
    [InlineData("scope_id", true)]
    [InlineData("tenant_id", true)]
    [InlineData("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", true)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("scope id", false)]
    [InlineData("tenant\tid", false)]
    public void Validates_claim_name_shape(string value, bool expected)
    {
        Assert.Equal(expected, GmaClaimNames.IsValidClaimName(value));
    }

    [Fact]
    public void Rejects_overlong_claim_names()
    {
        Assert.False(GmaClaimNames.IsValidClaimName(new string('x', GmaClaimNames.MaxLength + 1)));
    }
}
