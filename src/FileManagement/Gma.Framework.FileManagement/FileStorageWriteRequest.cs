namespace Gma.Framework.FileManagement;

public sealed record FileStorageWriteRequest
{
    public FileStorageWriteRequest(
        FileStorageObjectKey key,
        Stream content,
        long contentLength,
        string? contentType,
        string? fileName = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (contentLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(contentLength), contentLength, "Content length cannot be negative.");
        }

        if (!content.CanRead)
        {
            throw new ArgumentException("Content stream must be readable.", nameof(content));
        }

        this.Key = key;
        this.Content = content;
        this.ContentLength = contentLength;
        this.ContentType = FileStorageMetadata.ContentTypeOrDefault(contentType);
        this.FileName = FileStorageMetadata.NormalizeFileName(fileName);
        this.Metadata = FileStorageMetadata.NormalizeMetadata(metadata);
    }

    public FileStorageObjectKey Key { get; }
    public Stream Content { get; }
    public long ContentLength { get; }
    public string ContentType { get; }
    public string? FileName { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; }
}
