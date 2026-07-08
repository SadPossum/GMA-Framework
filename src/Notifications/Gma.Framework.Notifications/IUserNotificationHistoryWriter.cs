namespace Gma.Framework.Notifications;

public interface IUserNotificationHistoryWriter
{
    ValueTask SaveAsync(UserNotificationMessage message, CancellationToken cancellationToken = default);
}
