namespace Gma.Framework.Notifications;

public sealed class NotificationsOptions
{
    public const string SectionName = "Notifications";
    public const int DefaultSubscriberQueueCapacity = 128;
    public const int DefaultMaximumPayloadBytes = 32768;

    public bool Enabled { get; set; }
    public int SubscriberQueueCapacity { get; set; } = DefaultSubscriberQueueCapacity;
    public int MaximumPayloadBytes { get; set; } = DefaultMaximumPayloadBytes;
}
