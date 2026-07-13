namespace Gma.Framework.Notifications;

public sealed record NotificationSinkDeliveryRequest
{
    public NotificationSinkDeliveryRequest(
        Guid deliveryId,
        UserNotificationMessage message,
        int attempt,
        bool isDurable)
    {
        if (deliveryId == Guid.Empty)
        {
            throw new ArgumentException("Delivery id must not be empty.", nameof(deliveryId));
        }

        ArgumentNullException.ThrowIfNull(message);
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);

        this.DeliveryId = deliveryId;
        this.Message = message;
        this.Attempt = attempt;
        this.IsDurable = isDurable;
    }

    public Guid DeliveryId { get; }
    public UserNotificationMessage Message { get; }
    public int Attempt { get; }
    public bool IsDurable { get; }

    public static NotificationSinkDeliveryRequest BestEffort(UserNotificationMessage message) =>
        new(message.Id, message, attempt: 1, isDurable: false);
}
