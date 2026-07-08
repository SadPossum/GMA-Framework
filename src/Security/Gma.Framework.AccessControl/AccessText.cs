namespace Gma.Framework.AccessControl;

using System.Diagnostics.CodeAnalysis;
internal static class AccessText
{
    public static string NormalizeIdentifier(
        string? value,
        int maxLength,
        string description,
        string parameterName)
    {
        if (TryNormalizeIdentifier(value, maxLength, out string? normalized))
        {
            return normalized;
        }

        throw new ArgumentException(
            $"{description} is required, must be {maxLength} characters or fewer, and cannot contain whitespace or control characters.",
            parameterName);
    }

    public static bool TryNormalizeIdentifier(
        string? value,
        int maxLength,
        [NotNullWhen(true)] out string? normalized)
    {
        normalized = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string candidate = value.Trim();
        if (candidate.Length > maxLength ||
            candidate.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            return false;
        }

        normalized = candidate;
        return true;
    }
}
