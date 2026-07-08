namespace Gma.Framework.Notifications;

internal static class NotificationVersions
{
    public static int Normalize(int version, string parameterName)
    {
        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, version, "Notification version must be positive.");
        }

        return version;
    }
}
