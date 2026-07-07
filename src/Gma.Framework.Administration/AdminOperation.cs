namespace Gma.Framework.Administration;

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

public sealed partial record AdminOperation
{
    public const int MaxLength = 256;

    private AdminOperation(string name, AdminPermission permission)
    {
        this.Name = name;
        this.Permission = permission;
    }

    public string Name { get; }
    public AdminPermission Permission { get; }

    public static AdminOperation Create(string name, AdminPermission permission)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Admin operation name is required.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(permission);

        string normalized = name.Trim().ToLowerInvariant();

        if (normalized.Length > MaxLength)
        {
            throw new ArgumentException($"Admin operation name must be {MaxLength} characters or fewer.", nameof(name));
        }

        if (!OperationNameRegex().IsMatch(normalized))
        {
            throw new ArgumentException("Admin operation name must be dot-separated lowercase words.", nameof(name));
        }

        return new AdminOperation(normalized, permission);
    }

    public static bool TryCreate(string? name, AdminPermission permission, [NotNullWhen(true)] out AdminOperation? operation)
    {
        operation = null;
        ArgumentNullException.ThrowIfNull(permission);

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        string normalized = name.Trim().ToLowerInvariant();

        if (normalized.Length > MaxLength || !OperationNameRegex().IsMatch(normalized))
        {
            return false;
        }

        operation = new AdminOperation(normalized, permission);
        return true;
    }

    [GeneratedRegex(@"^[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*)+$")]
    private static partial Regex OperationNameRegex();
}
