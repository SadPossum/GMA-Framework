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
    private FileContentCapabilityReadiness(bool isReady, FileContentCapabilityProvider provider)
    {
        this.IsReady = isReady;
        this.Provider = provider;
    }

    public bool IsReady { get; }
    public FileContentCapabilityProvider Provider { get; }

    public static FileContentCapabilityReadiness Ready(string provider) =>
        new(true, FileContentCapabilityProvider.Create(provider));

    public static FileContentCapabilityReadiness Unavailable(string provider) =>
        new(false, FileContentCapabilityProvider.Create(provider));
}

public readonly record struct FileContentCapabilityProvider
{
    private readonly string value;

    private FileContentCapabilityProvider(string value) => this.value = value;

    public string Value => this.value ?? string.Empty;

    public static FileContentCapabilityProvider Create(string value) =>
        new(FileContentCapabilityIdentity.Normalize(value, nameof(value)));

    public override string ToString() => this.Value;
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
