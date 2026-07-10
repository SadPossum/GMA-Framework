namespace Gma.Framework.Tests;

using Microsoft.Extensions.Options;
using Gma.Framework.Administration.Api;
using Gma.Framework.Security;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdminApiOptionsValidatorTests
{
    private readonly AdminApiOptionsValidator validator = new();

    [Fact]
    public void Validate_accepts_default_settings()
    {
        ValidateOptionsResult result = this.validator.Validate(name: null, new AdminApiOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_rejects_missing_actor_id_claim()
    {
        ValidateOptionsResult result = this.validator.Validate(
            name: null,
            new AdminApiOptions { ActorIdClaim = string.Empty });

        AssertFailure(result, "ActorIdClaim");
    }

    [Theory]
    [InlineData("actor id")]
    [InlineData("actor\tid")]
    public void Validate_rejects_invalid_actor_id_claim(string actorIdClaim)
    {
        ValidateOptionsResult result = this.validator.Validate(
            name: null,
            new AdminApiOptions { ActorIdClaim = actorIdClaim });

        AssertFailure(result, "ActorIdClaim");
    }

    [Fact]
    public void Validate_rejects_overlong_actor_id_claim()
    {
        ValidateOptionsResult result = this.validator.Validate(
            name: null,
            new AdminApiOptions { ActorIdClaim = new string('x', GmaClaimNames.MaxLength + 1) });

        AssertFailure(result, "ActorIdClaim");
    }

    [Fact]
    public void Validate_rejects_missing_tenant_id_claim_when_claim_match_is_required()
    {
        ValidateOptionsResult result = this.validator.Validate(
            name: null,
            new AdminApiOptions
            {
                RequireTenantClaimMatch = true,
                TenantIdClaim = string.Empty
            });

        AssertFailure(result, "TenantIdClaim");
    }

    [Theory]
    [InlineData("scope id")]
    [InlineData("tenant\tid")]
    public void Validate_rejects_invalid_tenant_id_claim(string TenantIdClaim)
    {
        ValidateOptionsResult result = this.validator.Validate(
            name: null,
            new AdminApiOptions
            {
                RequireTenantClaimMatch = false,
                TenantIdClaim = TenantIdClaim
            });

        AssertFailure(result, "TenantIdClaim");
    }

    [Fact]
    public void Validate_rejects_overlong_tenant_id_claim()
    {
        ValidateOptionsResult result = this.validator.Validate(
            name: null,
            new AdminApiOptions
            {
                RequireTenantClaimMatch = false,
                TenantIdClaim = new string('x', GmaClaimNames.MaxLength + 1)
            });

        AssertFailure(result, "TenantIdClaim");
    }

    [Fact]
    public void Validate_allows_missing_tenant_id_claim_when_claim_match_is_disabled()
    {
        ValidateOptionsResult result = this.validator.Validate(
            name: null,
            new AdminApiOptions
            {
                RequireTenantClaimMatch = false,
                TenantIdClaim = string.Empty
            });

        Assert.True(result.Succeeded);
    }

    private static void AssertFailure(ValidateOptionsResult result, string expectedFailure)
    {
        Assert.True(result.Failed);
        Assert.Contains(expectedFailure, result.FailureMessage, StringComparison.Ordinal);
    }
}
