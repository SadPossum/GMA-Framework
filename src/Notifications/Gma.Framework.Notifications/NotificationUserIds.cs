namespace Gma.Framework.Notifications;

using System.Diagnostics.CodeAnalysis;

public static class NotificationUserIds
{
    public const int MaxLength = 256;

    public static string Normalize(string userId, string parameterName = "userId")
    {
        if (TryNormalize(userId, out string? normalized))
        {
            return normalized;
        }

        throw new ArgumentException(
            $"User notification user id is required, must be {MaxLength} characters or fewer, and cannot contain control characters.",
            parameterName);
    }

    public static bool TryNormalize(
        string? userId,
        [NotNullWhen(true)] out string? normalized)
    {
        normalized = null;

        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        string candidate = userId.Trim();
        if (candidate.Length > MaxLength ||
            candidate.Any(char.IsControl))
        {
            return false;
        }

        normalized = candidate;
        return true;
    }
}
