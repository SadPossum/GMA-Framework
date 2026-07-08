namespace Gma.Framework.FileManagement;

using System.Diagnostics.CodeAnalysis;

public readonly record struct FileStorageObjectKey
{
    public const int MaxLength = 1024;

    private readonly string? value;

    public FileStorageObjectKey(string value)
        => this.value = Normalize(value);

    public string Value => this.value ?? string.Empty;

    public static bool TryCreate(
        string? value,
        [NotNullWhen(true)] out FileStorageObjectKey? objectKey)
    {
        objectKey = null;
        if (!TryNormalize(value, out string? normalized))
        {
            return false;
        }

        objectKey = new FileStorageObjectKey(normalized);
        return true;
    }

    public override string ToString() => this.Value;

    public static string Normalize(string value)
    {
        if (TryNormalize(value, out string? normalized))
        {
            return normalized;
        }

        throw new ArgumentException(
            $"Object key must be 1-{MaxLength} characters, use safe ASCII path segments, and cannot contain '.', '..', or empty segments.",
            nameof(value));
    }

    public static bool TryNormalize(
        string? value,
        [NotNullWhen(true)] out string? normalized)
    {
        normalized = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string candidate = value.Trim().Replace('\\', '/');
        if (candidate.Length == 0 ||
            candidate.Length > MaxLength ||
            candidate.StartsWith('/') ||
            candidate.EndsWith('/') ||
            candidate.Contains("//", StringComparison.Ordinal) ||
            candidate.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            return false;
        }

        string[] segments = candidate.Split('/');
        if (segments.Any(segment => segment is "." or ".." || !IsSafeSegment(segment)))
        {
            return false;
        }

        normalized = candidate;
        return true;
    }

    private static bool IsSafeSegment(string segment) =>
        segment.Length > 0 &&
        segment.All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '-' or '_' or '.');
}
