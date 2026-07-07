namespace Gma.Framework.Tenancy.Infrastructure;

using Gma.Framework.Naming;
using Microsoft.Extensions.Options;
using Gma.Framework.Tenancy;

internal sealed class TenantOptionsValidator : IValidateOptions<TenantOptions>
{
    public ValidateOptionsResult Validate(string? name, TenantOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.HeaderName))
        {
            return ValidateOptionsResult.Fail($"{TenantOptions.SectionName}:HeaderName is required.");
        }

        if (!IsHttpToken(options.HeaderName))
        {
            return ValidateOptionsResult.Fail($"{TenantOptions.SectionName}:HeaderName must be a valid HTTP header name.");
        }

        if (!TenantIds.TryNormalize(options.LocalDefaultTenantId, out _))
        {
            return ValidateOptionsResult.Fail(
                $"{TenantOptions.SectionName}:LocalDefaultTenantId is required, must be {TenantIds.MaxLength} characters or fewer, and cannot contain whitespace or control characters.");
        }

        return ValidateOptionsResult.Success;
    }

    private static bool IsHttpToken(string value)
    {
        foreach (char character in value)
        {
            if (!IsHttpTokenCharacter(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsHttpTokenCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) ||
        character is '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-' or '.' or '^' or '_' or '`' or '|' or '~';
}
