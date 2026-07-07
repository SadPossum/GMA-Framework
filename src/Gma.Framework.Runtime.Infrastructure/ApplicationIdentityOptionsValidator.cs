namespace Gma.Framework.Runtime.Infrastructure;

using Microsoft.Extensions.Options;
using Gma.Framework.Naming;
using Gma.Framework.Runtime;

public sealed class ApplicationIdentityOptionsValidator : IValidateOptions<ApplicationIdentityOptions>
{
    public ValidateOptionsResult Validate(string? name, ApplicationIdentityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.DisplayName))
        {
            return ValidateOptionsResult.Fail($"{ApplicationIdentityOptions.SectionName}:DisplayName is required.");
        }

        if (!ApplicationNamespaces.IsValid(options.Namespace))
        {
            return ValidateOptionsResult.Fail(
                $"{ApplicationIdentityOptions.SectionName}:Namespace must be a lowercase kebab-case value " +
                $"with {ApplicationNamespaces.MaxLength} characters or fewer.");
        }

        return ValidateOptionsResult.Success;
    }
}
