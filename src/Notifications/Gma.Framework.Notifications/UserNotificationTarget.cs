namespace Gma.Framework.Notifications;

using Gma.Framework.Naming;

public sealed record UserNotificationTarget
{
    private UserNotificationTarget(string scopeId, string userId)
    {
        this.ScopeId = ScopeIds.Normalize(scopeId);
        this.UserId = NotificationUserIds.Normalize(userId);
    }

    public string ScopeId { get; }
    public string UserId { get; }

    public static UserNotificationTarget User(string scopeId, string userId) => new(scopeId, userId);
}
