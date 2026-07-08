namespace Gma.Framework.Notifications;

public interface IUserNotificationFeed
{
    IUserNotificationSubscription Subscribe(
        UserNotificationTarget target,
        CancellationToken cancellationToken = default);
}
