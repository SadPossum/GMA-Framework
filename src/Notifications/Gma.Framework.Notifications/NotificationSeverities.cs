namespace Gma.Framework.Notifications;

internal static class NotificationSeverities
{
    public static NotificationSeverity Normalize(NotificationSeverity severity, string parameterName)
    {
        if (!Enum.IsDefined(severity) || severity == NotificationSeverity.Unknown)
        {
            throw new ArgumentOutOfRangeException(parameterName, severity, "Notification severity is not supported.");
        }

        return severity;
    }
}
