namespace Gma.Framework.Tests.FileManagement;

using Gma.Framework.FileManagement.Minio;
using Xunit;

[Trait("Category", "Unit")]
public sealed class MinioFileStorageOptionsTests
{
    [Fact]
    public void Production_rejects_plaintext_transport_by_default()
    {
        string[] failures = MinioFileStorageOptionsValidation.Validate(
            CreateOptions(useSsl: false, createBucket: false),
            isProduction: true);

        Assert.Contains(
            failures,
            failure => failure.Contains("HTTPS", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_rejects_automatic_bucket_creation_by_default()
    {
        string[] failures = MinioFileStorageOptionsValidation.Validate(
            CreateOptions(useSsl: true, createBucket: true),
            isProduction: true);

        Assert.Contains(
            failures,
            failure => failure.Contains("CreateBucketIfMissing", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_accepts_explicit_private_network_exceptions()
    {
        MinioFileStorageOptions options = CreateOptions(
            useSsl: false,
            createBucket: true);
        options.AllowInsecureTransportInProduction = true;
        options.AllowBucketCreationInProduction = true;

        string[] failures = MinioFileStorageOptionsValidation.Validate(
            options,
            isProduction: true);

        Assert.Empty(failures);
    }

    [Fact]
    public void Absolute_endpoint_scheme_is_authoritative()
    {
        MinioFileStorageOptions options = CreateOptions(
            useSsl: true,
            createBucket: false);
        options.Endpoint = "http://minio.internal:9000";

        string[] failures = MinioFileStorageOptionsValidation.Validate(
            options,
            isProduction: true);

        Assert.Contains(
            failures,
            failure => failure.Contains("HTTPS", StringComparison.Ordinal));
    }

    private static MinioFileStorageOptions CreateOptions(
        bool useSsl,
        bool createBucket) =>
        new()
        {
            Endpoint = "minio.internal:9000",
            AccessKey = "service-account",
            SecretKey = "not-a-real-secret",
            BucketName = "example-files",
            UseSsl = useSsl,
            CreateBucketIfMissing = createBucket
        };
}
