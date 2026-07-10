namespace Gma.Framework.Permissions;

using System.Diagnostics.CodeAnalysis;
using Gma.Framework.Naming;

public sealed record PermissionCode
{
    public const int MaxLength = 256;

    private PermissionCode(string value) => this.Value = value;

    public string Value { get; }

    public static PermissionCode Create(string code)
    {
        if (TryCreate(code, out PermissionCode? permission))
        {
            return permission;
        }

        throw new ArgumentException(
            "Permission code is required, must be a dot-separated lowercase code, and must be 256 characters or fewer.",
            nameof(code));
    }

    public static bool TryCreate(string? code, [NotNullWhen(true)] out PermissionCode? permission)
    {
        permission = null;

        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        string normalized = code.Trim().ToLowerInvariant();
        if (normalized.Length > MaxLength)
        {
            return false;
        }

        string[] segments = normalized.Split('.');
        if (segments.Length < 2)
        {
            return false;
        }

        if (segments.Any(segment => !SharedNameSegments.IsKebabSegment(segment)))
        {
            return false;
        }

        permission = new PermissionCode(normalized);
        return true;
    }

    public override string ToString() => this.Value;
}
