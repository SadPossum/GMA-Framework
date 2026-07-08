namespace Gma.Framework.FileManagement.Minio;

using Microsoft.Extensions.Options;

internal sealed class MinioFileStorageOptionsValidator : IValidateOptions<MinioFileStorageOptions>
{
    public ValidateOptionsResult Validate(string? name, MinioFileStorageOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint) ||
            options.Endpoint.Length > MinioFileStorageOptions.EndpointMaxLength ||
            options.Endpoint.Any(char.IsControl))
        {
            return ValidateOptionsResult.Fail(
                $"{MinioFileStorageOptions.SectionName}:Endpoint is required and must be {MinioFileStorageOptions.EndpointMaxLength} characters or fewer.");
        }

        if (!MinioFileStorageOptions.TryCreateHttpEndpoint(options.Endpoint, out _) &&
            !Uri.TryCreate($"{(options.UseSsl ? Uri.UriSchemeHttps : Uri.UriSchemeHttp)}://{options.Endpoint}", UriKind.Absolute, out _))
        {
            return ValidateOptionsResult.Fail(
                $"{MinioFileStorageOptions.SectionName}:Endpoint must be a valid host[:port] or absolute URI.");
        }

        if (!IsCredential(options.AccessKey))
        {
            return ValidateOptionsResult.Fail(
                $"{MinioFileStorageOptions.SectionName}:AccessKey is required and must be {MinioFileStorageOptions.CredentialMaxLength} characters or fewer.");
        }

        if (!IsCredential(options.SecretKey))
        {
            return ValidateOptionsResult.Fail(
                $"{MinioFileStorageOptions.SectionName}:SecretKey is required and must be {MinioFileStorageOptions.CredentialMaxLength} characters or fewer.");
        }

        if (!IsValidBucketName(options.BucketName))
        {
            return ValidateOptionsResult.Fail(
                $"{MinioFileStorageOptions.SectionName}:BucketName must be an S3-compatible bucket name.");
        }

        return ValidateOptionsResult.Success;
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
}
