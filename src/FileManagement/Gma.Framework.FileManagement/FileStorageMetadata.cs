namespace Gma.Framework.FileManagement;

using System.Diagnostics.CodeAnalysis;

public static class FileStorageMetadata
{
    public const int FileNameMaxLength = 255;
    public const int MetadataKeyMaxLength = 64;
    public const int MetadataValueMaxLength = 512;
    public const int MetadataEntriesMaxCount = 16;

    public static string ContentTypeOrDefault(string? contentType) =>
        TryNormalizeContentType(contentType, out string? normalized)
            ? normalized
            : "application/octet-stream";

    public static bool IsValidContentType(string? contentType) =>
        TryNormalizeContentType(contentType, out _);

    public static bool TryNormalizeContentType(
        string? contentType,
        [NotNullWhen(true)] out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        string candidate = contentType.Trim().ToLowerInvariant();
        if (candidate.Length > FileManagementOptions.ContentTypeMaxLength ||
            candidate.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)) ||
            !candidate.Contains('/', StringComparison.Ordinal))
        {
            return false;
        }

        normalized = candidate;
        return true;
    }

    public static string? NormalizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        string candidate = Path.GetFileName(fileName.Trim());
        if (candidate.Length == 0 ||
            candidate.Length > FileNameMaxLength ||
            candidate.Any(char.IsControl))
        {
            return null;
        }

        return candidate;
    }

    public static IReadOnlyDictionary<string, string> NormalizeMetadata(
        IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        if (metadata.Count > MetadataEntriesMaxCount)
        {
            throw new ArgumentException(
                $"Metadata can contain at most {MetadataEntriesMaxCount} entries.",
                nameof(metadata));
        }

        Dictionary<string, string> normalized = new(StringComparer.Ordinal);
        foreach ((string key, string value) in metadata.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            if (!IsValidMetadataKey(key))
            {
                throw new ArgumentException(
                    $"Metadata keys must be 1-{MetadataKeyMaxLength} characters and use lowercase letters, digits, '-' or '.'.",
                    nameof(metadata));
            }

            if (!IsValidMetadataValue(value))
            {
                throw new ArgumentException(
                    $"Metadata values must be {MetadataValueMaxLength} characters or fewer and cannot contain control characters.",
                    nameof(metadata));
            }

            normalized[key] = value.Trim();
        }

        return normalized;
    }

    private static bool IsValidMetadataKey(string? key) =>
        !string.IsNullOrWhiteSpace(key) &&
        key.Length <= MetadataKeyMaxLength &&
        key.All(character =>
            (character >= 'a' && character <= 'z') ||
            char.IsAsciiDigit(character) ||
            character is '-' or '.');

    private static bool IsValidMetadataValue(string? value) =>
        value is not null &&
        value.Length <= MetadataValueMaxLength &&
        value.All(character => !char.IsControl(character));
}
