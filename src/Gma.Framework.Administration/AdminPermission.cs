namespace Gma.Framework.Administration;

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "AdminPermission is the public administration contract name.")]
public sealed partial record AdminPermission
{
    public const int MaxLength = 256;
    public const string OwnerWildcard = "*";

    private AdminPermission(string code) => this.Code = code;

    public string Code { get; }

    public static AdminPermission Create(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Admin permission code is required.", nameof(code));
        }

        string normalized = code.Trim().ToLowerInvariant();

        if (normalized.Length > MaxLength)
        {
            throw new ArgumentException($"Admin permission code must be {MaxLength} characters or fewer.", nameof(code));
        }

        if (normalized != OwnerWildcard && !PermissionCodeRegex().IsMatch(normalized))
        {
            throw new ArgumentException("Admin permission code must be dot-separated lowercase words.", nameof(code));
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

        if (normalized.Length > MaxLength ||
            (normalized != OwnerWildcard && !PermissionCodeRegex().IsMatch(normalized)))
        {
            return false;
        }

        permission = new AdminPermission(normalized);
        return true;
    }

    public override string ToString() => this.Code;

    [GeneratedRegex(@"^[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*)+$")]
    private static partial Regex PermissionCodeRegex();
}
