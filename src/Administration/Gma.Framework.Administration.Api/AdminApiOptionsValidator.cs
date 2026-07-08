namespace Gma.Framework.Administration.Api;

using Microsoft.Extensions.Options;
using Gma.Framework.Security;

internal sealed class AdminApiOptionsValidator : IValidateOptions<AdminApiOptions>
{
    public ValidateOptionsResult Validate(string? name, AdminApiOptions options)
    {
        if (!ApplicationClaimNames.IsValidClaimName(options.ActorIdClaim))
        {
            return ValidateOptionsResult.Fail(
                $"{AdminApiOptions.SectionName}:ActorIdClaim must be 1-{ApplicationClaimNames.MaxLength} characters and cannot contain whitespace or control characters.");
        }

        if (options.RequireTenantClaimMatch && string.IsNullOrWhiteSpace(options.TenantIdClaim))
        {
            return ValidateOptionsResult.Fail(
                $"{AdminApiOptions.SectionName}:TenantIdClaim is required when RequireTenantClaimMatch is true.");
        }

        if (!string.IsNullOrWhiteSpace(options.TenantIdClaim) &&
            !ApplicationClaimNames.IsValidClaimName(options.TenantIdClaim))
        {
            return ValidateOptionsResult.Fail(
                $"{AdminApiOptions.SectionName}:TenantIdClaim must be 1-{ApplicationClaimNames.MaxLength} characters and cannot contain whitespace or control characters.");
        }

        return ValidateOptionsResult.Success;
    }
}
