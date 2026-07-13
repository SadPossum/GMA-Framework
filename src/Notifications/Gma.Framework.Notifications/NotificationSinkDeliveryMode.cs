namespace Gma.Framework.Notifications;

[Flags]
public enum NotificationSinkDeliveryMode
{
    None = 0,
    BestEffort = 1,
    Durable = 2
}
