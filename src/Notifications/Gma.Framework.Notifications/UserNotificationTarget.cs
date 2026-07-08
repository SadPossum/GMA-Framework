namespace Gma.Framework.Notifications;

using Gma.Framework.Naming;

public sealed record UserNotificationTarget
{
    private UserNotificationTarget(string tenantId, string userId)
    {
        this.TenantId = TenantIds.Normalize(tenantId);
        this.UserId = NotificationUserIds.Normalize(userId);
    }

    public string TenantId { get; }
    public string UserId { get; }

    public static UserNotificationTarget User(string tenantId, string userId) => new(tenantId, userId);
}
