namespace Gma.Framework.Realtime;

public interface IRealtimeSink<TMessage>
{
    string ProviderName { get; }

    ValueTask DeliverAsync(RealtimeChannel channel, TMessage message, CancellationToken cancellationToken);
}
