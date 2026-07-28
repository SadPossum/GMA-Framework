namespace Gma.Framework.Api.Production;

using Gma.Framework.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

internal sealed class ProductionDataProtectionOptionsValidator(
    IHostEnvironment environment,
    IConfiguration configuration)
    : IValidateOptions<ProductionDataProtectionOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        ProductionDataProtectionOptions options)
    {
        string[] failures = ProductionDataProtectionOptionsValidation.Validate(
            options,
            configuration[$"{ApplicationIdentityOptions.SectionName}:Namespace"],
            environment.IsProduction());

        return failures.Length == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

internal static class ProductionDataProtectionOptionsValidation
{
    public static string[] Validate(
        ProductionDataProtectionOptions options,
        string? applicationNamespace,
        bool isProduction)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];
        string applicationName = ResolveApplicationName(options, applicationNamespace);
        if (string.IsNullOrWhiteSpace(applicationName) ||
            applicationName.Length > ProductionDataProtectionOptions.ApplicationNameMaxLength ||
            applicationName.Any(char.IsControl))
        {
            failures.Add(
                "DataProtection:ApplicationName or ApplicationIdentity:Namespace must provide a stable application name with 128 characters or fewer.");
        }

        bool keyRingRequired = isProduction || options.RequirePersistentKeys;
        if (keyRingRequired && string.IsNullOrWhiteSpace(options.KeyRingPath))
        {
            failures.Add(
                "DataProtection:KeyRingPath is required in Production or when persistent keys are required.");
        }
        else if (!string.IsNullOrWhiteSpace(options.KeyRingPath) &&
                 (options.KeyRingPath.Length > ProductionDataProtectionOptions.KeyRingPathMaxLength ||
                  options.KeyRingPath.Any(char.IsControl)))
        {
            failures.Add(
                "DataProtection:KeyRingPath must be 1024 characters or fewer and cannot contain control characters.");
        }

        return [.. failures];
    }

    public static string ResolveApplicationName(
        ProductionDataProtectionOptions options,
        string? applicationNamespace) =>
        !string.IsNullOrWhiteSpace(options.ApplicationName)
            ? options.ApplicationName.Trim()
            : applicationNamespace?.Trim() ?? string.Empty;
}
