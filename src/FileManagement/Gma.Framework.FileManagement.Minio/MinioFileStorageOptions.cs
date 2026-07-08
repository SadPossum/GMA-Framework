namespace Gma.Framework.FileManagement.Minio;

public sealed class MinioFileStorageOptions
{
    public const string SectionName = "FileManagement:Minio";
    public const int EndpointMaxLength = 512;
    public const int CredentialMaxLength = 256;
    public const int BucketNameMaxLength = 63;

    public string Endpoint { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = "generic-modular-api-files";
    public bool UseSsl { get; set; } = true;
    public bool CreateBucketIfMissing { get; set; } = true;

    internal Uri ToEndpointUri()
    {
        string endpoint = this.Endpoint.Trim();
        if (TryCreateHttpEndpoint(endpoint, out Uri? absolute) && absolute is not null)
        {
            return absolute;
        }

        string scheme = this.UseSsl ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
        return new Uri($"{scheme}://{endpoint}", UriKind.Absolute);
    }

    internal static bool TryCreateHttpEndpoint(string endpoint, out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? absolute) ||
            absolute.Host.Length == 0 ||
            (!string.Equals(absolute.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) &&
             !string.Equals(absolute.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)))
        {
            return false;
        }

        uri = absolute;
        return true;
    }
}
