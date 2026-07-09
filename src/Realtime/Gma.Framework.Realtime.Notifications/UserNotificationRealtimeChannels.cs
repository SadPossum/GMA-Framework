namespace Gma.Framework.Realtime.Notifications;

using Gma.Framework.Notifications;
using Gma.Framework.Realtime;

internal static class UserNotificationRealtimeChannels
{
    public static RealtimeChannel ForTarget(UserNotificationTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return ForUser(target.TenantId, target.UserId);
    }

    public static RealtimeChannel ForMessage(UserNotificationMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return ForUser(message.TenantId, message.UserId);
    }

    private static RealtimeChannel ForUser(string tenantId, string userId) =>
        RealtimeChannel.Create("notifications-user", tenantId, userId);
}
