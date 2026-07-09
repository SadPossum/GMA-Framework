namespace Gma.Framework.Realtime;

public sealed class RealtimeSubscriberQueueOptions<TMessage>
{
    public const int DefaultSubscriberQueueCapacity = 128;
    public const int MaximumSubscriberQueueCapacity = 10_000;

    public int SubscriberQueueCapacity { get; set; } = DefaultSubscriberQueueCapacity;
}
