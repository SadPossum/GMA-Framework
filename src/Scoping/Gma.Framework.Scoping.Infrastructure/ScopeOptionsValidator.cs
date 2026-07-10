namespace Gma.Framework.Scoping.Infrastructure;

using Gma.Framework.Naming;
using Gma.Framework.Scoping;
using Microsoft.Extensions.Options;

internal sealed class ScopeOptionsValidator : IValidateOptions<ScopeOptions>
{
    public ValidateOptionsResult Validate(string? name, ScopeOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.HeaderName))
        {
            return ValidateOptionsResult.Fail($"{ScopeOptions.SectionName}:HeaderName is required.");
        }

        if (!IsHttpToken(options.HeaderName))
        {
            return ValidateOptionsResult.Fail($"{ScopeOptions.SectionName}:HeaderName must be a valid HTTP header name.");
        }

        if (!ScopeIds.TryNormalize(options.LocalDefaultScopeId, out _))
        {
            return ValidateOptionsResult.Fail(
                $"{ScopeOptions.SectionName}:LocalDefaultScopeId is required, must be {ScopeIds.MaxLength} characters or fewer, and cannot contain whitespace or control characters.");
        }

        return ValidateOptionsResult.Success;
    }

    private static bool IsHttpToken(string value) =>
        value.Length > 0 &&
        value.All(character =>
            character is >= '!' and <= '~'
                and not ('(' or ')' or '<' or '>' or '@' or ',' or ';' or ':' or '\\' or '"' or '/' or '[' or ']' or '?' or '=' or '{' or '}'));
}
