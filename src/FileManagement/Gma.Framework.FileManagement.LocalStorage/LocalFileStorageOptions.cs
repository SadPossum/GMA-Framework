namespace Gma.Framework.FileManagement.LocalStorage;

public sealed class LocalFileStorageOptions
{
    public const string SectionName = "FileManagement:LocalStorage";
    public const int RootPathMaxLength = 512;

    public string RootPath { get; set; } = "data/files";
}
