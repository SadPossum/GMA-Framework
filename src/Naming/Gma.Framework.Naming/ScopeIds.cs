namespace Gma.Framework.Naming;

using System.Diagnostics.CodeAnalysis;

public static class ScopeIds
{
    public const int MaxLength = 128;

    public static string Normalize(string scopeId, string parameterName = "scopeId")
    {
        if (TryNormalize(scopeId, out string? normalized))
        {
            return normalized;
        }

        throw new ArgumentException(
            $"Scope id is required, must be {MaxLength} characters or fewer, and cannot contain whitespace or control characters.",
            parameterName);
    }

    public static bool TryNormalize(
        string? scopeId,
        [NotNullWhen(true)] out string? normalized)
    {
        normalized = null;

        if (string.IsNullOrWhiteSpace(scopeId))
        {
            return false;
        }

        string candidate = scopeId.Trim();
        if (candidate.Length > MaxLength ||
            candidate.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            return false;
        }

        normalized = candidate;
        return true;
    }
}
