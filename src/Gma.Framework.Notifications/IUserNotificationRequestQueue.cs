namespace Gma.Framework.Notifications;

#pragma warning disable CA1711 // Queue describes deferred, scoped notification-request semantics.
public interface IUserNotificationRequestQueue
{
    ValueTask EnqueueAsync<TPayload>(
        string moduleName,
        UserNotificationTarget target,
        TPayload payload,
        NotificationPublishOptions options,
        CancellationToken cancellationToken = default)
        where TPayload : IUserNotificationPayload;
}
#pragma warning restore CA1711
