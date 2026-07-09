namespace Gma.Framework.Realtime.Notifications;

using Gma.Framework.Notifications;
using Gma.Framework.Realtime;

internal sealed class UserNotificationRealtimeSink(
    IRealtimeSink<UserNotificationMessage> sink) : IUserNotificationSink
{
    public string ProviderName => sink.ProviderName;

    public ValueTask DeliverAsync(UserNotificationMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        return sink.DeliverAsync(
            UserNotificationRealtimeChannels.ForMessage(message),
            message,
            cancellationToken);
    }
}
