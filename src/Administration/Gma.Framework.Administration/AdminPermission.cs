namespace Gma.Framework.Administration;

using System.Diagnostics.CodeAnalysis;
using Gma.Framework.Naming;

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "AdminPermission is the public administration contract name.")]
public sealed record AdminPermission
{
    public const int MaxLength = 256;

    private AdminPermission(string code) => this.Code = code;

    public string Code { get; }

    public static AdminPermission Create(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Admin permission code is required.", nameof(code));
        }

        string normalized = code.Trim().ToLowerInvariant();

        if (!IsPermissionCode(normalized))
        {
            throw new ArgumentException(
                "Admin permission code is required, must be a dot-separated lowercase code, and must be 256 characters or fewer.",
                nameof(code));
        }

        return new AdminPermission(normalized);
    }

    public static bool TryCreate(string? code, [NotNullWhen(true)] out AdminPermission? permission)
    {
        permission = null;

        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        string normalized = code.Trim().ToLowerInvariant();

        if (!IsPermissionCode(normalized))
        {
            return false;
        }

        permission = new AdminPermission(normalized);
        return true;
    }

    public override string ToString() => this.Code;

    private static bool IsPermissionCode(string code)
    {
        if (code.Length > MaxLength)
        {
            return false;
        }

        string[] segments = code.Split('.');
        return segments.Length >= 2 &&
            segments.All(segment => SharedNameSegments.IsKebabSegment(segment));
    }
}
