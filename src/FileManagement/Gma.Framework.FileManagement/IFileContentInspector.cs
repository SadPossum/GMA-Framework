namespace Gma.Framework.FileManagement;

public interface IFileContentInspector
{
    ValueTask<FileContentInspectionResult> InspectAsync(
        FileContentInspectionRequest request,
        CancellationToken cancellationToken);
}

public sealed record FileContentInspectionRequest(
    Stream Content,
    long ContentLength,
    string ContentType,
    string? FileName);

public sealed record FileContentInspectionResult(FileContentInspectionStatus Status, string Inspector)
{
    public static FileContentInspectionResult Clean(string inspector) =>
        new(FileContentInspectionStatus.Clean, NormalizeInspector(inspector));

    public static FileContentInspectionResult Rejected(string inspector) =>
        new(FileContentInspectionStatus.Rejected, NormalizeInspector(inspector));

    public static FileContentInspectionResult Unavailable(string inspector) =>
        new(FileContentInspectionStatus.Unavailable, NormalizeInspector(inspector));

    private static string NormalizeInspector(string inspector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inspector);
        string candidate = inspector.Trim();
        if (candidate.Length > FileStorageMetadata.MetadataValueMaxLength || candidate.Any(char.IsControl))
        {
            throw new ArgumentException("Inspector name is not valid metadata.", nameof(inspector));
        }

        return candidate;
    }
}

public enum FileContentInspectionStatus
{
    Unavailable = 0,
    Clean = 1,
    Rejected = 2
}
