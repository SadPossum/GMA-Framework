namespace Gma.Framework.Realtime;

public interface IRealtimeSubscription<out TMessage> : IAsyncDisposable
{
    RealtimeChannel Channel { get; }

    IAsyncEnumerable<TMessage> ReadAllAsync(CancellationToken cancellationToken = default);
}
