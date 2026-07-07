namespace Gma.Framework.Notifications.Infrastructure;

using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Gma.Framework.Notifications;

internal sealed class InMemoryUserNotificationBus(
    IOptions<NotificationsOptions> options,
    ILogger<InMemoryUserNotificationBus> logger) : IUserNotificationFeed, IUserNotificationSink
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Subscriber>> subscribers = new(
        StringComparer.Ordinal);

    public string ProviderName => "memory";

    public IUserNotificationSubscription Subscribe(
        UserNotificationTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();

        BoundedChannelOptions channelOptions = new(Math.Max(1, options.Value.SubscriberQueueCapacity))
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        };
        Channel<UserNotificationMessage> channel = Channel.CreateBounded<UserNotificationMessage>(channelOptions);
        Guid subscriptionId = Guid.NewGuid();
        string key = CreateTargetKey(target);
        Subscriber subscriber = new(subscriptionId, channel);
        ConcurrentDictionary<Guid, Subscriber> targetSubscribers = this.subscribers.GetOrAdd(
            key,
            static _ => new ConcurrentDictionary<Guid, Subscriber>());
        if (!targetSubscribers.TryAdd(subscriptionId, subscriber))
        {
            throw new InvalidOperationException("Could not create a user notification stream subscription.");
        }

        CancellationTokenRegistration registration = cancellationToken.Register(
            static state =>
            {
                CancellationRegistrationState registrationState = (CancellationRegistrationState)state!;
                registrationState.Owner.Remove(registrationState.Key, registrationState.SubscriptionId);
                registrationState.Channel.Writer.TryComplete();
            },
            new CancellationRegistrationState(this, key, subscriptionId, channel));

        return new InMemoryUserNotificationSubscription(this, target, key, subscriptionId, channel, registration);
    }

    public ValueTask DeliverAsync(UserNotificationMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        string key = CreateTargetKey(message.TenantId, message.UserId);
        if (!this.subscribers.TryGetValue(key, out ConcurrentDictionary<Guid, Subscriber>? targetSubscribers))
        {
            return ValueTask.CompletedTask;
        }

        foreach (Subscriber subscriber in targetSubscribers.Values)
        {
            if (!subscriber.Channel.Writer.TryWrite(message))
            {
                logger.LogDebug(
                    "Notification stream subscriber {SubscriptionId} for tenant {TenantId} and user {UserId} could not accept notification {NotificationId}.",
                    subscriber.Id,
                    message.TenantId,
                    message.UserId,
                    message.Id);
            }
        }

        return ValueTask.CompletedTask;
    }

    private void Remove(string key, Guid subscriptionId)
    {
        if (!this.subscribers.TryGetValue(key, out ConcurrentDictionary<Guid, Subscriber>? targetSubscribers))
        {
            return;
        }

        targetSubscribers.TryRemove(subscriptionId, out _);
        if (targetSubscribers.IsEmpty)
        {
            this.subscribers.TryRemove(key, out _);
        }
    }

    private static string CreateTargetKey(UserNotificationTarget target) => CreateTargetKey(target.TenantId, target.UserId);

    private static string CreateTargetKey(string tenantId, string userId) => $"{tenantId}\u001f{userId}";

    private sealed record Subscriber(Guid Id, Channel<UserNotificationMessage> Channel);

    private sealed record CancellationRegistrationState(
        InMemoryUserNotificationBus Owner,
        string Key,
        Guid SubscriptionId,
        Channel<UserNotificationMessage> Channel);

    private sealed class InMemoryUserNotificationSubscription(
        InMemoryUserNotificationBus owner,
        UserNotificationTarget target,
        string key,
        Guid subscriptionId,
        Channel<UserNotificationMessage> channel,
        CancellationTokenRegistration cancellationRegistration) : IUserNotificationSubscription
    {
        private int disposed;

        public UserNotificationTarget Target { get; } = target;

        public IAsyncEnumerable<UserNotificationMessage> ReadAllAsync(CancellationToken cancellationToken = default) =>
            channel.Reader.ReadAllAsync(cancellationToken);

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) == 1)
            {
                return ValueTask.CompletedTask;
            }

            cancellationRegistration.Dispose();
            owner.Remove(key, subscriptionId);
            channel.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }
}
