namespace Gma.Framework.FileManagement;

public sealed record FileStorageObjectProperties(
    FileStorageObjectKey Key,
    long ContentLength,
    string ContentType,
    string? FileName,
    string? ETag,
    DateTimeOffset? LastModifiedUtc,
    IReadOnlyDictionary<string, string> Metadata);
