namespace Gma.Framework.Realtime.Notifications;

using Gma.Framework.Notifications;
using Gma.Framework.Realtime;

internal sealed class UserNotificationRealtimeFeed(
    IRealtimeFeed<UserNotificationMessage> feed) : IUserNotificationFeed
{
    public IUserNotificationSubscription Subscribe(
        UserNotificationTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);

        IRealtimeSubscription<UserNotificationMessage> subscription = feed.Subscribe(
            UserNotificationRealtimeChannels.ForTarget(target),
            cancellationToken);
        return new UserNotificationRealtimeSubscription(target, subscription);
    }

    private sealed class UserNotificationRealtimeSubscription(
        UserNotificationTarget target,
        IRealtimeSubscription<UserNotificationMessage> subscription) : IUserNotificationSubscription
    {
        public UserNotificationTarget Target { get; } = target;

        public IAsyncEnumerable<UserNotificationMessage> ReadAllAsync(CancellationToken cancellationToken = default) =>
            subscription.ReadAllAsync(cancellationToken);

        public ValueTask DisposeAsync() => subscription.DisposeAsync();
    }
}
