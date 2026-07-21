namespace Gma.Framework.FileManagement;

public interface IFileContentTypeDetector
{
    ValueTask<FileContentTypeDetectionResult> DetectAsync(
        FileContentTypeDetectionRequest request,
        CancellationToken cancellationToken);
}

public sealed record FileContentTypeDetectionRequest(
    Stream Content,
    long ContentLength,
    string? FileName);

public sealed record FileContentTypeDetectionResult
{
    private FileContentTypeDetectionResult(
        FileContentTypeDetectionStatus status,
        string detector,
        string? contentType)
    {
        this.Status = status;
        this.Detector = FileContentCapabilityIdentity.Normalize(detector, nameof(detector));
        this.ContentType = contentType;
    }

    public FileContentTypeDetectionStatus Status { get; }
    public string Detector { get; }
    public string? ContentType { get; }

    public static FileContentTypeDetectionResult Detected(string detector, string contentType)
    {
        if (!FileStorageMetadata.TryNormalizeContentType(contentType, out string? normalizedContentType))
        {
            throw new ArgumentException("Detected content type is invalid.", nameof(contentType));
        }

        return new FileContentTypeDetectionResult(
            FileContentTypeDetectionStatus.Detected,
            detector,
            normalizedContentType);
    }

    public static FileContentTypeDetectionResult Unrecognized(string detector) =>
        new(FileContentTypeDetectionStatus.Unrecognized, detector, contentType: null);

    public static FileContentTypeDetectionResult Unavailable(string detector) =>
        new(FileContentTypeDetectionStatus.Unavailable, detector, contentType: null);
}

public enum FileContentTypeDetectionStatus
{
    Unavailable = 0,
    Detected = 1,
    Unrecognized = 2
}
