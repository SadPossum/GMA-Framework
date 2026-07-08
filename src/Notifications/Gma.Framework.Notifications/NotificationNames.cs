namespace Gma.Framework.Notifications;

using Gma.Framework.Naming;

public static class NotificationNames
{
    public const int MaxLength = 128;

    public static string NormalizeName(string value, string parameterName = "notificationName")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        string normalized = value.Trim().ToLowerInvariant();
        if (!IsValidName(normalized))
        {
            throw new ArgumentException(
                $"{parameterName} must be dot-separated lowercase kebab-case notification name segments.",
                parameterName);
        }

        return normalized;
    }

    public static bool IsValidName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Trim().ToLowerInvariant();
        return normalized.Length <= MaxLength &&
               normalized
                   .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   .Length == normalized.Count(character => character == '.') + 1 &&
               normalized
                   .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   .All(SharedNameSegments.IsKebabSegment);
    }
}
