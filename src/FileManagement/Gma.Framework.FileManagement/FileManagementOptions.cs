namespace Gma.Framework.FileManagement;

public sealed class FileManagementOptions
{
    public const string SectionName = "FileManagement";
    public const long DefaultMaximumObjectBytes = 10 * 1024 * 1024;
    public const int ContentTypeMaxLength = 255;

    public bool Enabled { get; set; }
    public FileStorageProvider Provider { get; set; } = FileStorageProvider.Unknown;
    public long MaximumObjectBytes { get; set; } = DefaultMaximumObjectBytes;
    public string[] AllowedContentTypes { get; set; } = [];
    public bool RequireContentInspection { get; set; }
}
