namespace Gma.Framework.Naming;

using System.Diagnostics.CodeAnalysis;

public static class TenantIds
{
    public const int MaxLength = 128;

    public static string Normalize(string tenantId)
    {
        if (TryNormalize(tenantId, out string? normalized))
        {
            return normalized;
        }

        throw new ArgumentException(
            $"Tenant id is required, must be {MaxLength} characters or fewer, and cannot contain whitespace or control characters.",
            nameof(tenantId));
    }

    public static bool TryNormalize(
        string? tenantId,
        [NotNullWhen(true)] out string? normalized)
    {
        normalized = null;

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return false;
        }

        string candidate = tenantId.Trim();
        if (candidate.Length > MaxLength ||
            candidate.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            return false;
        }

        normalized = candidate;
        return true;
    }
}
