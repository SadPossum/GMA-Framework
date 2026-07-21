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
        new(FileContentInspectionStatus.Clean, FileContentCapabilityIdentity.Normalize(inspector, nameof(inspector)));

    public static FileContentInspectionResult Rejected(string inspector) =>
        new(FileContentInspectionStatus.Rejected, FileContentCapabilityIdentity.Normalize(inspector, nameof(inspector)));

    public static FileContentInspectionResult Unavailable(string inspector) =>
        new(FileContentInspectionStatus.Unavailable, FileContentCapabilityIdentity.Normalize(inspector, nameof(inspector)));
}

public enum FileContentInspectionStatus
{
    Unavailable = 0,
    Clean = 1,
    Rejected = 2
}
