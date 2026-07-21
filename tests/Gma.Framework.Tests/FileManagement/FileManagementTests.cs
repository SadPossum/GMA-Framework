namespace Gma.Framework.Tests;

using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Gma.Framework.FileManagement;
using Gma.Framework.FileManagement.LocalStorage;
using Xunit;

[Trait("Category", "Unit")]
public sealed class FileManagementTests
{
    [Fact]
    public void Object_keys_reject_path_traversal_and_whitespace()
    {
        Assert.True(FileStorageObjectKey.TryCreate("files/tenant-123/avatar", out FileStorageObjectKey? safeKey));
        Assert.Equal("files/tenant-123/avatar", safeKey.Value.Value);

        Assert.False(FileStorageObjectKey.TryCreate("files/../secret", out _));
        Assert.False(FileStorageObjectKey.TryCreate("/files/avatar", out _));
        Assert.False(FileStorageObjectKey.TryCreate("files//avatar", out _));
        Assert.False(FileStorageObjectKey.TryCreate("files/avatar name", out _));
    }

    [Fact]
    public void File_management_options_validation_rejects_invalid_enabled_configuration()
    {
        string[] failures = FileManagementOptionsValidation.Validate(new FileManagementOptions
        {
            Enabled = true,
            Provider = FileStorageProvider.Unknown,
            MaximumObjectBytes = 0,
            AllowedContentTypes = ["text/plain", "not-a-content-type"]
        });

        Assert.Contains(failures, failure => failure.Contains("Provider", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("MaximumObjectBytes", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("AllowedContentTypes", StringComparison.Ordinal));
    }

    [Fact]
    public void File_management_options_validation_rejects_null_content_type_allowlist()
    {
        string[] failures = FileManagementOptionsValidation.Validate(new FileManagementOptions
        {
            AllowedContentTypes = null!
        });

        Assert.Contains(failures, failure => failure.Contains("AllowedContentTypes", StringComparison.Ordinal));
    }

    [Fact]
    public void Content_type_detection_results_normalize_trusted_metadata()
    {
        FileContentTypeDetectionResult result = FileContentTypeDetectionResult.Detected(
            " signature-detector ",
            " IMAGE/PNG ");

        Assert.Equal(FileContentTypeDetectionStatus.Detected, result.Status);
        Assert.Equal("signature-detector", result.Detector);
        Assert.Equal("image/png", result.ContentType);
        Assert.Throws<ArgumentException>(() =>
            FileContentTypeDetectionResult.Detected("signature-detector", "not-a-media-type"));
    }

    [Fact]
    public void Content_capability_readiness_exposes_only_normalized_identity_and_state()
    {
        FileContentCapabilityReadiness ready = FileContentCapabilityReadiness.Ready(" scanner-a ");
        FileContentCapabilityReadiness unavailable = FileContentCapabilityReadiness.Unavailable("detector-a");

        Assert.True(ready.IsReady);
        Assert.Equal("scanner-a", ready.Provider);
        Assert.False(unavailable.IsReady);
        Assert.Equal("detector-a", unavailable.Provider);
        Assert.Throws<ArgumentException>(() => FileContentCapabilityReadiness.Ready("scanner\rname"));
    }

    [Fact]
    public void Local_storage_registration_is_noop_when_file_management_is_disabled()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.AddLocalFileStorage();

        using ServiceProvider provider = builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IFileStorage>());
    }

    [Fact]
    public void Local_storage_registration_is_noop_for_another_selected_provider()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["FileManagement:Enabled"] = "true";
        builder.Configuration["FileManagement:Provider"] = "Minio";

        builder.AddLocalFileStorage();

        using ServiceProvider provider = builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IFileStorage>());
    }

    [Fact]
    public void Local_storage_registration_fails_fast_for_invalid_shared_options()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["FileManagement:Enabled"] = "true";
        builder.Configuration["FileManagement:Provider"] = "Unknown";

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(
            () => builder.AddLocalFileStorage());

        Assert.Contains(exception.Failures, failure => failure.Contains("Provider", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Local_storage_round_trips_content_and_metadata()
    {
        string root = Path.Combine(Path.GetTempPath(), $"gma-files-{Guid.NewGuid():N}");

        try
        {
            using ServiceProvider provider = BuildLocalProvider(root);
            IFileStorage storage = provider.GetRequiredService<IFileStorage>();
            FileStorageObjectKey key = new("files/global/avatar");
            byte[] payload = Encoding.UTF8.GetBytes("hello-files");

            await using MemoryStream input = new(payload);
            FileStorageObjectProperties stored = await storage.PutAsync(new FileStorageWriteRequest(
                key,
                input,
                payload.Length,
                "text/plain",
                "avatar.txt",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["module"] = "files"
                }));

            Assert.Equal(payload.Length, stored.ContentLength);
            Assert.Equal("text/plain", stored.ContentType);
            Assert.Equal("avatar.txt", stored.FileName);

            FileStorageReadResult? read = await storage.OpenReadAsync(key);
            Assert.NotNull(read);

            await using MemoryStream output = new();
            await read.CopyToAsync(output);

            Assert.Equal(payload, output.ToArray());
            Assert.True(await storage.DeleteAsync(key));
            Assert.Null(await storage.GetPropertiesAsync(key));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static ServiceProvider BuildLocalProvider(string root)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["FileManagement:Enabled"] = "true";
        builder.Configuration["FileManagement:Provider"] = "LocalStorage";
        builder.Configuration["FileManagement:MaximumObjectBytes"] = "1048576";
        builder.Configuration["FileManagement:LocalStorage:RootPath"] = root;
        builder.AddLocalFileStorage();
        return builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
