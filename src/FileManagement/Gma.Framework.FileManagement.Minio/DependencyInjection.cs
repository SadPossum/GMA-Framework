namespace Gma.Framework.FileManagement.Minio;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using global::Minio;
using Gma.Framework.FileManagement;
using Gma.Framework.ModuleComposition;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddMinioFileStorage(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        IConfigurationSection fileManagement = builder.Configuration.GetSection(FileManagementOptions.SectionName);
        FileManagementOptions fileManagementOptions = fileManagement.Get<FileManagementOptions>() ?? new FileManagementOptions();
        ValidateFileManagementOptions(fileManagementOptions);

        if (!fileManagementOptions.Enabled)
        {
            return builder;
        }

        if (fileManagementOptions.Provider != FileStorageProvider.Minio)
        {
            return builder;
        }

        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(MinioFileStorageRegistrationMarker)))
        {
            return builder;
        }

        IConfigurationSection minio = builder.Configuration.GetSection(MinioFileStorageOptions.SectionName);
        MinioFileStorageOptions minioOptions = minio.Get<MinioFileStorageOptions>() ?? new MinioFileStorageOptions();
        ValidateMinioOptions(minioOptions);

        builder.Services.AddSingleton<MinioFileStorageRegistrationMarker>();
        builder.ProvideFeature(new ProvidedCompositionFeature(
            new CompositionFeatureId(FileManagementCompositionFeatures.Storage),
            "Gma.Framework.FileManagement.Minio",
            "File storage backend services are registered."));
        builder.Services.AddSingleton<IMinioClient>(_ => BuildClient(minioOptions));
        builder.Services.AddSingleton<IFileStorage, MinioFileStorage>();
        builder.Services
            .AddOptions<FileManagementOptions>()
            .Bind(fileManagement)
            .Validate(IsValidFileManagementOptions, FileManagementOptionsValidation.FailureMessage)
            .ValidateOnStart();
        builder.Services
            .AddOptions<MinioFileStorageOptions>()
            .Bind(minio)
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<MinioFileStorageOptions>, MinioFileStorageOptionsValidator>());

        return builder;
    }

    private static IMinioClient BuildClient(MinioFileStorageOptions options)
    {
        Uri endpoint = options.ToEndpointUri();
        string host = endpoint.IsDefaultPort ? endpoint.Host : $"{endpoint.Host}:{endpoint.Port}";

        return new MinioClient()
            .WithEndpoint(host)
            .WithCredentials(options.AccessKey, options.SecretKey)
            .WithSSL(endpoint.Scheme == Uri.UriSchemeHttps)
            .Build();
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

    private static void ValidateMinioOptions(MinioFileStorageOptions options)
    {
        ValidateOptionsResult result = new MinioFileStorageOptionsValidator().Validate(name: null, options);
        if (result.Failed)
        {
            throw new OptionsValidationException(
                MinioFileStorageOptions.SectionName,
                typeof(MinioFileStorageOptions),
                result.Failures);
        }
    }

    private sealed class MinioFileStorageRegistrationMarker;
}
