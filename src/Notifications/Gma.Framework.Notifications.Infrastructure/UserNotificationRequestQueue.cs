namespace Gma.Framework.Notifications.Infrastructure;

using Microsoft.Extensions.Logging;
using Gma.Framework.Naming;
using Gma.Framework.Notifications;

internal interface IUserNotificationRequestQueueFlusher
{
    ValueTask FlushAsync(CancellationToken cancellationToken);
}

internal sealed class UserNotificationRequestQueue(
    IUserNotificationPublisher publisher,
    ILogger<UserNotificationRequestQueue> logger) : IUserNotificationRequestQueue, IUserNotificationRequestQueueFlusher
{
    private readonly List<QueuedNotificationRequest> requests = [];

    public ValueTask EnqueueAsync<TPayload>(
        string moduleName,
        UserNotificationTarget target,
        TPayload payload,
        NotificationPublishOptions options,
        CancellationToken cancellationToken = default)
        where TPayload : IUserNotificationPayload
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        string normalizedModuleName = SharedNameSegments.NormalizeKebabSegment(
            moduleName,
            "module name",
            nameof(moduleName));
        NotificationMetadataReader.NotificationMetadata metadata =
            NotificationMetadataReader.ReadRequired(payload.GetType());

        this.requests.Add(QueuedNotificationRequest.Create(
            normalizedModuleName,
            metadata.Name,
            target,
            payload,
            options));

        return ValueTask.CompletedTask;
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        while (this.requests.Count > 0)
        {
            QueuedNotificationRequest request = this.requests[0];
            this.requests.RemoveAt(0);

            try
            {
                await request.PublishAsync(publisher, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "User notification request failed open after commit for module {Module}, notification {NotificationName}, tenant {TenantId}, and user {UserId}.",
                    request.ModuleName,
                    request.NotificationName,
                    request.Target.TenantId,
                    request.Target.UserId);
            }
        }
    }

    private sealed record QueuedNotificationRequest(
        string ModuleName,
        string NotificationName,
        UserNotificationTarget Target,
        Func<IUserNotificationPublisher, CancellationToken, ValueTask> Publish)
    {
        public static QueuedNotificationRequest Create<TPayload>(
            string moduleName,
            string notificationName,
            UserNotificationTarget target,
            TPayload payload,
            NotificationPublishOptions options)
            where TPayload : IUserNotificationPayload =>
            new(
                moduleName,
                notificationName,
                target,
                (publisher, cancellationToken) => publisher.PublishAsync(
                    moduleName,
                    target,
                    payload,
                    options,
                    cancellationToken));

        public ValueTask PublishAsync(IUserNotificationPublisher publisher, CancellationToken cancellationToken) =>
            this.Publish(publisher, cancellationToken);
    }
}
