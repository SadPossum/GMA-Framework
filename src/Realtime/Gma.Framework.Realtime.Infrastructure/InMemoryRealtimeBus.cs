namespace Gma.Framework.Realtime.Infrastructure;

using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal sealed class InMemoryRealtimeBus<TMessage>(
    IOptions<RealtimeSubscriberQueueOptions<TMessage>> options,
    ILogger<InMemoryRealtimeBus<TMessage>> logger) :
    IRealtimeFeed<TMessage>,
    IRealtimeSink<TMessage>,
    IDisposable,
    IAsyncDisposable
    where TMessage : notnull
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Subscriber>> subscribers = new(
        StringComparer.Ordinal);
    private int disposed;

    public string ProviderName => "memory";

    public IRealtimeSubscription<TMessage> Subscribe(
        RealtimeChannel channel,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        cancellationToken.ThrowIfCancellationRequested();
        this.ThrowIfDisposed();

        BoundedChannelOptions channelOptions = new(options.Value.SubscriberQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        };
        Channel<TMessage> messageChannel = Channel.CreateBounded<TMessage>(channelOptions);
        Guid subscriptionId = Guid.NewGuid();
        Subscriber subscriber = new(subscriptionId, messageChannel);
        ConcurrentDictionary<Guid, Subscriber> channelSubscribers = this.subscribers.GetOrAdd(
            channel.Key,
            static _ => new ConcurrentDictionary<Guid, Subscriber>());
        if (!channelSubscribers.TryAdd(subscriptionId, subscriber))
        {
            throw new InvalidOperationException("Could not create a realtime stream subscription.");
        }
        if (this.IsDisposed)
        {
            this.Remove(channel.Key, subscriptionId);
            messageChannel.Writer.TryComplete();
            ObjectDisposedException.ThrowIf(this.IsDisposed, this);
        }

        CancellationTokenRegistration registration = cancellationToken.Register(
            static state =>
            {
                CancellationRegistrationState registrationState = (CancellationRegistrationState)state!;
                registrationState.Owner.Remove(registrationState.ChannelKey, registrationState.SubscriptionId);
                registrationState.MessageChannel.Writer.TryComplete();
            },
            new CancellationRegistrationState(this, channel.Key, subscriptionId, messageChannel));

        return new InMemoryRealtimeSubscription(this, channel, subscriptionId, messageChannel, registration);
    }

    public ValueTask DeliverAsync(RealtimeChannel channel, TMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();
        this.ThrowIfDisposed();

        if (!this.subscribers.TryGetValue(channel.Key, out ConcurrentDictionary<Guid, Subscriber>? channelSubscribers))
        {
            return ValueTask.CompletedTask;
        }

        foreach (Subscriber subscriber in channelSubscribers.Values)
        {
            if (!subscriber.MessageChannel.Writer.TryWrite(message))
            {
                logger.LogDebug(
                    "Realtime stream subscriber {SubscriptionId} for channel {ChannelName} could not accept a {MessageType} message.",
                    subscriber.Id,
                    channel.Name,
                    typeof(TMessage).Name);
            }
        }

        return ValueTask.CompletedTask;
    }

    private void Remove(string channelKey, Guid subscriptionId)
    {
        if (!this.subscribers.TryGetValue(channelKey, out ConcurrentDictionary<Guid, Subscriber>? channelSubscribers))
        {
            return;
        }

        channelSubscribers.TryRemove(subscriptionId, out _);
        if (channelSubscribers.IsEmpty)
        {
            this.subscribers.TryRemove(channelKey, out _);
        }
    }

    public ValueTask DisposeAsync()
    {
        this.DisposeCore();
        return ValueTask.CompletedTask;
    }

    public void Dispose() => this.DisposeCore();

    private void DisposeCore()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) == 1)
        {
            return;
        }

        foreach (ConcurrentDictionary<Guid, Subscriber> channelSubscribers in this.subscribers.Values)
        {
            foreach (Subscriber subscriber in channelSubscribers.Values)
            {
                subscriber.MessageChannel.Writer.TryComplete();
            }
        }

        this.subscribers.Clear();
    }

    private bool IsDisposed => Volatile.Read(ref this.disposed) == 1;

    private void ThrowIfDisposed()
    {
        if (this.IsDisposed)
        {
            ObjectDisposedException.ThrowIf(this.IsDisposed, this);
        }
    }

    private sealed record Subscriber(Guid Id, Channel<TMessage> MessageChannel);

    private sealed record CancellationRegistrationState(
        InMemoryRealtimeBus<TMessage> Owner,
        string ChannelKey,
        Guid SubscriptionId,
        Channel<TMessage> MessageChannel);

    private sealed class InMemoryRealtimeSubscription(
        InMemoryRealtimeBus<TMessage> owner,
        RealtimeChannel channel,
        Guid subscriptionId,
        Channel<TMessage> messageChannel,
        CancellationTokenRegistration cancellationRegistration) : IRealtimeSubscription<TMessage>
    {
        private int disposed;

        public RealtimeChannel Channel { get; } = channel;

        public IAsyncEnumerable<TMessage> ReadAllAsync(CancellationToken cancellationToken = default) =>
            messageChannel.Reader.ReadAllAsync(cancellationToken);

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) == 1)
            {
                return ValueTask.CompletedTask;
            }

            cancellationRegistration.Dispose();
            owner.Remove(this.Channel.Key, subscriptionId);
            messageChannel.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }
}
