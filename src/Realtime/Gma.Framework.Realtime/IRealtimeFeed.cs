namespace Gma.Framework.Realtime;

public interface IRealtimeFeed<TMessage>
{
    IRealtimeSubscription<TMessage> Subscribe(
        RealtimeChannel channel,
        CancellationToken cancellationToken = default);
}
