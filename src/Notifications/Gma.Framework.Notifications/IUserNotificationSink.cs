namespace Gma.Framework.Notifications;

public interface IUserNotificationSink
{
    string ProviderName { get; }
    IReadOnlyCollection<string> DeliveryTags { get; }
    NotificationSinkDeliveryMode DeliveryModes { get; }

    ValueTask<NotificationSinkDeliveryResult> DeliverAsync(
        NotificationSinkDeliveryRequest request,
        CancellationToken cancellationToken);
}
