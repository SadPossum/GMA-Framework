namespace Gma.Framework.Notifications;

internal static class NotificationDescriptions
{
    public const int MaxLength = 256;

    public static string Normalize(string description, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description, parameterName);

        string normalized = description.Trim();
        if (normalized.Length > MaxLength ||
            normalized.Any(character => char.IsControl(character)))
        {
            throw new ArgumentException(
                $"Notification descriptions must be {MaxLength} characters or fewer and cannot contain control characters.",
                parameterName);
        }

        return normalized;
    }
}
