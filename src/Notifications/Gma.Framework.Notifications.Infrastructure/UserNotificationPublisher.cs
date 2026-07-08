namespace Gma.Framework.Notifications.Infrastructure;

using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Gma.Framework.Naming;
using Gma.Framework.Notifications;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class UserNotificationPublisher(
    IEnumerable<IUserNotificationSink> sinks,
    IEnumerable<IUserNotificationHistoryWriter> historyWriters,
    IIdGenerator idGenerator,
    ISystemClock clock,
    IOptions<NotificationsOptions> options,
    NotificationMetrics metrics,
    ILogger<UserNotificationPublisher> logger) : IUserNotificationPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IUserNotificationSink[] sinks = sinks.ToArray();
    private readonly IUserNotificationHistoryWriter[] historyWriters = historyWriters.ToArray();

    public async ValueTask PublishAsync<TPayload>(
        string moduleName,
        UserNotificationTarget target,
        TPayload payload,
        NotificationPublishOptions publishOptions,
        CancellationToken cancellationToken = default)
        where TPayload : IUserNotificationPayload
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(publishOptions);
        cancellationToken.ThrowIfCancellationRequested();

        string normalizedModuleName = SharedNameSegments.NormalizeKebabSegment(moduleName, "module name", nameof(moduleName));
        NotificationMetadataReader.NotificationMetadata metadata =
            NotificationMetadataReader.ReadRequired(payload.GetType());

        if (!options.Value.Enabled && this.historyWriters.Length == 0)
        {
            metrics.RecordPublished(normalizedModuleName, metadata.Name, "bypass");
            return;
        }

        JsonElement payloadElement = SerializePayload(payload, options.Value.MaximumPayloadBytes);
        UserNotificationMessage message = new(
            publishOptions.Id ?? idGenerator.NewId(),
            normalizedModuleName,
            metadata.Name,
            metadata.Version,
            target.TenantId,
            target.UserId,
            publishOptions.Title,
            publishOptions.Body,
            publishOptions.Severity,
            publishOptions.OccurredAtUtc ?? clock.UtcNow,
            payloadElement);

        await this.SaveHistoryAsync(message, cancellationToken).ConfigureAwait(false);
        await this.PublishMessageAsync(message, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask SaveHistoryAsync(UserNotificationMessage message, CancellationToken cancellationToken)
    {
        foreach (IUserNotificationHistoryWriter historyWriter in this.historyWriters)
        {
            try
            {
                await historyWriter.SaveAsync(message, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "User notification {NotificationId} history persistence failed open for module {Module}, notification {NotificationName}, tenant {TenantId}, and user {UserId}.",
                    message.Id,
                    message.Module,
                    message.Name,
                    message.TenantId,
                    message.UserId);
            }
        }
    }

    private async ValueTask PublishMessageAsync(UserNotificationMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        if (!options.Value.Enabled)
        {
            metrics.RecordPublished(message.Module, message.Name, "bypass");
            return;
        }

        metrics.RecordPublished(message.Module, message.Name, "success");

        foreach (IUserNotificationSink sink in this.sinks)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                await sink.DeliverAsync(message, cancellationToken).ConfigureAwait(false);
                metrics.RecordDelivery(message.Module, message.Name, sink.ProviderName, "success", stopwatch.Elapsed);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                metrics.RecordDelivery(message.Module, message.Name, sink.ProviderName, "failure", stopwatch.Elapsed);
                logger.LogWarning(
                    exception,
                    "User notification {NotificationId} delivery failed open through {NotificationProvider} for module {Module}, notification {NotificationName}, tenant {TenantId}, and user {UserId}.",
                    message.Id,
                    sink.ProviderName,
                    message.Module,
                    message.Name,
                    message.TenantId,
                    message.UserId);
            }
        }
    }

    private static JsonElement SerializePayload<TPayload>(TPayload payload, int maximumPayloadBytes)
        where TPayload : IUserNotificationPayload
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType(), JsonOptions);
        if (bytes.Length > maximumPayloadBytes)
        {
            throw new ArgumentException(
                $"Notification payload is {bytes.Length} bytes and exceeds the configured {maximumPayloadBytes} byte limit.",
                nameof(payload));
        }

        using JsonDocument document = JsonDocument.Parse(bytes);
        return document.RootElement.Clone();
    }
}
