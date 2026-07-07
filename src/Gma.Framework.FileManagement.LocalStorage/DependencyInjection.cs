namespace Gma.Framework.FileManagement.LocalStorage;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Gma.Framework.FileManagement;
using Gma.Framework.ModuleComposition;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddLocalFileStorage(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        IConfigurationSection fileManagement = builder.Configuration.GetSection(FileManagementOptions.SectionName);
        FileManagementOptions fileManagementOptions = fileManagement.Get<FileManagementOptions>() ?? new FileManagementOptions();
        ValidateFileManagementOptions(fileManagementOptions);

        if (!fileManagementOptions.Enabled)
        {
            return builder;
        }

        if (fileManagementOptions.Provider != FileStorageProvider.LocalStorage)
        {
            return builder;
        }

        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(LocalFileStorageRegistrationMarker)))
        {
            return builder;
        }

        IConfigurationSection localStorage = builder.Configuration.GetSection(LocalFileStorageOptions.SectionName);
        LocalFileStorageOptions localStorageOptions = localStorage.Get<LocalFileStorageOptions>() ?? new LocalFileStorageOptions();
        ValidateLocalStorageOptions(localStorageOptions);

        builder.Services.AddSingleton<LocalFileStorageRegistrationMarker>();
        builder.ProvideFeature(new ProvidedCompositionFeature(
            new CompositionFeatureId(FileManagementCompositionFeatures.Storage),
            "Gma.Framework.FileManagement.LocalStorage",
            "File storage backend services are registered."));
        builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
        builder.Services
            .AddOptions<FileManagementOptions>()
            .Bind(fileManagement)
            .Validate(IsValidFileManagementOptions, FileManagementOptionsValidation.FailureMessage)
            .ValidateOnStart();
        builder.Services
            .AddOptions<LocalFileStorageOptions>()
            .Bind(localStorage)
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<LocalFileStorageOptions>, LocalFileStorageOptionsValidator>());

        return builder;
    }

    private static void ValidateFileManagementOptions(FileManagementOptions options)
    {
        string[] failures = FileManagementOptionsValidation.Validate(options);
        if (failures.Length > 0)
        {
            throw new OptionsValidationException(
                FileManagementOptions.SectionName,
                typeof(FileManagementOptions),
                failures);
        }
    }

    private static bool IsValidFileManagementOptions(FileManagementOptions options) =>
        FileManagementOptionsValidation.Validate(options).Length == 0;

    private static void ValidateLocalStorageOptions(LocalFileStorageOptions options)
    {
        ValidateOptionsResult result = new LocalFileStorageOptionsValidator().Validate(name: null, options);
        if (result.Failed)
        {
            throw new OptionsValidationException(
                LocalFileStorageOptions.SectionName,
                typeof(LocalFileStorageOptions),
                result.Failures);
        }
    }

    private sealed class LocalFileStorageRegistrationMarker;
}
