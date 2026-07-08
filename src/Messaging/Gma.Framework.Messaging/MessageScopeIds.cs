namespace Gma.Framework.Messaging;

public static class MessageScopeIds
{
    public const int MaxLength = 128;

    public static string? NormalizeOptional(string? scopeId, string parameterName = "scopeId")
    {
        if (string.IsNullOrWhiteSpace(scopeId))
        {
            return null;
        }

        string normalized = scopeId.Trim();
        if (normalized.Length > MaxLength ||
            normalized.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            throw new ArgumentException(
                $"Message scope id must be {MaxLength} characters or fewer and cannot contain whitespace or control characters.",
                parameterName);
        }

        return normalized;
    }
}
