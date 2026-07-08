namespace Gma.Framework.Notifications;

public interface IUserNotificationPublisher
{
    ValueTask PublishAsync<TPayload>(
        string moduleName,
        UserNotificationTarget target,
        TPayload payload,
        NotificationPublishOptions options,
        CancellationToken cancellationToken = default)
        where TPayload : IUserNotificationPayload;
}
