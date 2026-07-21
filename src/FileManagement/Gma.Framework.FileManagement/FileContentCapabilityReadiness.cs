namespace Gma.Framework.FileManagement;

public interface IFileContentInspectorReadiness
{
    ValueTask<FileContentCapabilityReadiness> CheckReadinessAsync(CancellationToken cancellationToken);
}

public interface IFileContentTypeDetectorReadiness
{
    ValueTask<FileContentCapabilityReadiness> CheckReadinessAsync(CancellationToken cancellationToken);
}

public sealed record FileContentCapabilityReadiness
{
    private FileContentCapabilityReadiness(bool isReady, string provider)
    {
        this.IsReady = isReady;
        this.Provider = FileContentCapabilityIdentity.Normalize(provider, nameof(provider));
    }

    public bool IsReady { get; }
    public string Provider { get; }

    public static FileContentCapabilityReadiness Ready(string provider) => new(true, provider);

    public static FileContentCapabilityReadiness Unavailable(string provider) => new(false, provider);
}

internal static class FileContentCapabilityIdentity
{
    public static string Normalize(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        string candidate = value.Trim();
        if (candidate.Length > FileStorageMetadata.MetadataValueMaxLength || candidate.Any(char.IsControl))
        {
            throw new ArgumentException("File-content capability identity is not valid metadata.", parameterName);
        }

        return candidate;
    }
}
