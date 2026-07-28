namespace Gma.Framework.FileManagement.Minio;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

internal sealed class MinioFileStorageOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<MinioFileStorageOptions>
{
    public ValidateOptionsResult Validate(string? name, MinioFileStorageOptions options)
    {
        string[] failures = MinioFileStorageOptionsValidation.Validate(
            options,
            environment.IsProduction());
        return failures.Length == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

internal static class MinioFileStorageOptionsValidation
{
    public static string[] Validate(
        MinioFileStorageOptions options,
        bool isProduction)
    {
        List<string> failures = [];

        if (string.IsNullOrWhiteSpace(options.Endpoint) ||
            options.Endpoint.Length > MinioFileStorageOptions.EndpointMaxLength ||
            options.Endpoint.Any(char.IsControl))
        {
            failures.Add(
                $"{MinioFileStorageOptions.SectionName}:Endpoint is required and must be {MinioFileStorageOptions.EndpointMaxLength} characters or fewer.");
        }
        else if (!MinioFileStorageOptions.TryCreateHttpEndpoint(options.Endpoint, out _) &&
            !Uri.TryCreate($"{(options.UseSsl ? Uri.UriSchemeHttps : Uri.UriSchemeHttp)}://{options.Endpoint}", UriKind.Absolute, out _))
        {
            failures.Add(
                $"{MinioFileStorageOptions.SectionName}:Endpoint must be a valid host[:port] or absolute URI.");
        }

        if (!IsCredential(options.AccessKey))
        {
            failures.Add(
                $"{MinioFileStorageOptions.SectionName}:AccessKey is required and must be {MinioFileStorageOptions.CredentialMaxLength} characters or fewer.");
        }

        if (!IsCredential(options.SecretKey))
        {
            failures.Add(
                $"{MinioFileStorageOptions.SectionName}:SecretKey is required and must be {MinioFileStorageOptions.CredentialMaxLength} characters or fewer.");
        }

        if (!IsValidBucketName(options.BucketName))
        {
            failures.Add(
                $"{MinioFileStorageOptions.SectionName}:BucketName must be an S3-compatible bucket name.");
        }

        if (isProduction &&
            !UsesSecureTransport(options) &&
            !options.AllowInsecureTransportInProduction)
        {
            failures.Add(
                $"{MinioFileStorageOptions.SectionName} requires HTTPS in Production unless AllowInsecureTransportInProduction is explicitly enabled.");
        }

        if (isProduction &&
            options.CreateBucketIfMissing &&
            !options.AllowBucketCreationInProduction)
        {
            failures.Add(
                $"{MinioFileStorageOptions.SectionName}:CreateBucketIfMissing is not allowed in Production unless AllowBucketCreationInProduction is explicitly enabled.");
        }

        return [.. failures];
    }

    private static bool IsCredential(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= MinioFileStorageOptions.CredentialMaxLength &&
        value.All(character => !char.IsControl(character));

    private static bool IsValidBucketName(string? bucketName)
    {
        if (string.IsNullOrWhiteSpace(bucketName) ||
            bucketName.Length is < 3 or > MinioFileStorageOptions.BucketNameMaxLength ||
            bucketName.StartsWith('.') ||
            bucketName.EndsWith('.') ||
            bucketName.StartsWith('-') ||
            bucketName.EndsWith('-') ||
            bucketName.Contains("..", StringComparison.Ordinal) ||
            bucketName.Contains(".-", StringComparison.Ordinal) ||
            bucketName.Contains("-.", StringComparison.Ordinal))
        {
            return false;
        }

        return bucketName.All(character =>
            (character >= 'a' && character <= 'z') ||
            char.IsAsciiDigit(character) ||
            character is '-' or '.');
    }

    private static bool UsesSecureTransport(MinioFileStorageOptions options) =>
        MinioFileStorageOptions.TryCreateHttpEndpoint(options.Endpoint, out Uri? endpoint)
            ? string.Equals(endpoint?.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            : options.UseSsl;
}
