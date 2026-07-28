namespace Gma.Framework.Api.Production;

using Gma.Framework.Runtime;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public static class ProductionDataProtectionDependencyInjection
{
    public static IHostApplicationBuilder AddGmaProductionDataProtection(
        this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Services.Any(descriptor =>
                descriptor.ServiceType == typeof(ProductionDataProtectionRegistrationMarker)))
        {
            return builder;
        }

        IConfigurationSection section = builder.Configuration
            .GetSection(ProductionDataProtectionOptions.SectionName);
        ProductionDataProtectionOptions options =
            section.Get<ProductionDataProtectionOptions>() ?? new();
        string? applicationNamespace =
            builder.Configuration[$"{ApplicationIdentityOptions.SectionName}:Namespace"];
        string[] failures = ProductionDataProtectionOptionsValidation.Validate(
            options,
            applicationNamespace,
            builder.Environment.IsProduction());
        if (failures.Length > 0)
        {
            throw new OptionsValidationException(
                ProductionDataProtectionOptions.SectionName,
                typeof(ProductionDataProtectionOptions),
                failures);
        }

        builder.Services.AddSingleton<ProductionDataProtectionRegistrationMarker>();
        builder.Services
            .AddOptions<ProductionDataProtectionOptions>()
            .Bind(section)
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IValidateOptions<ProductionDataProtectionOptions>,
                ProductionDataProtectionOptionsValidator>());

        string applicationName =
            ProductionDataProtectionOptionsValidation.ResolveApplicationName(
                options,
                applicationNamespace);
        IDataProtectionBuilder dataProtection = builder.Services
            .AddDataProtection()
            .SetApplicationName(applicationName);

        if (!string.IsNullOrWhiteSpace(options.KeyRingPath))
        {
            string keyRingPath = Path.GetFullPath(
                options.KeyRingPath.Trim(),
                builder.Environment.ContentRootPath);
            Directory.CreateDirectory(keyRingPath);
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));
        }

        return builder;
    }

    private sealed class ProductionDataProtectionRegistrationMarker;
}
