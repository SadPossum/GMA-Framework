namespace Gma.Framework.Notifications;

public interface IUserNotificationSink
{
    string ProviderName { get; }

    ValueTask DeliverAsync(UserNotificationMessage message, CancellationToken cancellationToken);
}
