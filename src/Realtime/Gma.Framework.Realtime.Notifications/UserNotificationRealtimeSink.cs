namespace Gma.Framework.Realtime.Notifications;

using Gma.Framework.Notifications;
using Gma.Framework.Realtime;

internal sealed class UserNotificationRealtimeSink(
    IRealtimeSink<UserNotificationMessage> sink) : IUserNotificationSink
{
    public string ProviderName => sink.ProviderName;
    public IReadOnlyCollection<string> DeliveryTags { get; } = [NotificationTags.Web];
    public NotificationSinkDeliveryMode DeliveryModes => NotificationSinkDeliveryMode.BestEffort;

    public async ValueTask<NotificationSinkDeliveryResult> DeliverAsync(
        NotificationSinkDeliveryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        UserNotificationMessage message = request.Message;

        await sink.DeliverAsync(
            UserNotificationRealtimeChannels.ForMessage(message),
            message,
            cancellationToken).ConfigureAwait(false);

        return NotificationSinkDeliveryResult.Delivered();
    }
}
