namespace Gma.Framework.Notifications.Infrastructure;

using System.Diagnostics;
using System.Text.Json;
using Gma.Framework.Naming;
using Gma.Framework.Notifications;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal sealed class UserNotificationPublisher(
    IEnumerable<IUserNotificationSink> sinks,
    IEnumerable<IUserNotificationHistoryWriter> historyWriters,
    IEnumerable<IUserNotificationDeliveryPolicyEvaluator> deliveryPolicyEvaluators,
    IIdGenerator idGenerator,
    ISystemClock clock,
    IOptions<NotificationsOptions> options,
    NotificationMetrics metrics,
    ILogger<UserNotificationPublisher> logger) : IUserNotificationPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IUserNotificationSink[] sinks = sinks.ToArray();
    private readonly IUserNotificationHistoryWriter[] historyWriters = historyWriters.ToArray();
    private readonly IUserNotificationDeliveryPolicyEvaluator[] deliveryPolicyEvaluators = deliveryPolicyEvaluators.ToArray();

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
            target.ScopeId,
            target.UserId,
            publishOptions.Title,
            publishOptions.Body,
            publishOptions.Severity,
            publishOptions.OccurredAtUtc ?? clock.UtcNow,
            payloadElement,
            publishOptions.Tags,
            publishOptions.DeliveryPolicy);

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
                    "User notification {NotificationId} history persistence failed open for module {Module}, notification {NotificationName}, tenant {ScopeId}, and user {UserId}.",
                    message.Id,
                    message.Module,
                    message.Name,
                    message.ScopeId,
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

        string[] requestedDeliveryTags = NotificationTags.GetDeliveryTags(message.Tags).ToArray();
        foreach (IUserNotificationSink sink in this.sinks.Where(candidate =>
                     candidate.DeliveryModes.HasFlag(NotificationSinkDeliveryMode.BestEffort)))
        {
            string[] matchingTags = GetMatchingDeliveryTags(sink, requestedDeliveryTags);
            if (matchingTags.Length == 0)
            {
                continue;
            }

            if (!await this.ShouldDeliverAsync(message, sink, matchingTags, cancellationToken).ConfigureAwait(false))
            {
                metrics.RecordDelivery(message.Module, message.Name, sink.ProviderName, "bypass", TimeSpan.Zero);
                continue;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                NotificationSinkDeliveryResult result = await sink
                    .DeliverAsync(NotificationSinkDeliveryRequest.BestEffort(message), cancellationToken)
                    .ConfigureAwait(false);
                string metricResult = result.Outcome switch
                {
                    NotificationSinkDeliveryOutcome.Delivered => "success",
                    NotificationSinkDeliveryOutcome.Skipped => "bypass",
                    NotificationSinkDeliveryOutcome.Retry => "retry",
                    NotificationSinkDeliveryOutcome.Rejected => "rejected",
                    _ => "failure"
                };
                metrics.RecordDelivery(message.Module, message.Name, sink.ProviderName, metricResult, stopwatch.Elapsed);

                if (result.Outcome is NotificationSinkDeliveryOutcome.Retry or NotificationSinkDeliveryOutcome.Rejected)
                {
                    logger.LogWarning(
                        "User notification {NotificationId} delivery through {NotificationProvider} returned {DeliveryOutcome} with code {DeliveryCode} for module {Module} and notification {NotificationName}.",
                        message.Id,
                        sink.ProviderName,
                        result.Outcome,
                        result.Code,
                        message.Module,
                        message.Name);
                }
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
                    "User notification {NotificationId} delivery failed open through {NotificationProvider} for module {Module}, notification {NotificationName}, tenant {ScopeId}, and user {UserId}.",
                    message.Id,
                    sink.ProviderName,
                    message.Module,
                    message.Name,
                    message.ScopeId,
                    message.UserId);
            }
        }
    }

    private async ValueTask<bool> ShouldDeliverAsync(
        UserNotificationMessage message,
        IUserNotificationSink sink,
        IReadOnlyCollection<string> matchingTags,
        CancellationToken cancellationToken)
    {
        foreach (string deliveryTag in matchingTags)
        {
            bool allowed = true;
            foreach (IUserNotificationDeliveryPolicyEvaluator evaluator in this.deliveryPolicyEvaluators)
            {
                try
                {
                    if (!await evaluator
                            .ShouldDeliverAsync(message, deliveryTag, cancellationToken)
                            .ConfigureAwait(false))
                    {
                        allowed = false;
                        break;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    allowed = false;
                    logger.LogWarning(
                        "User notification {NotificationId} delivery policy for {NotificationProvider} and {DeliveryTag} failed closed with {ExceptionType}.",
                        message.Id,
                        sink.ProviderName,
                        deliveryTag,
                        exception.GetType().Name);
                    break;
                }
            }

            if (allowed)
            {
                return true;
            }
        }

        return false;
    }

    private static string[] GetMatchingDeliveryTags(
        IUserNotificationSink sink,
        IReadOnlyCollection<string> requestedDeliveryTags)
    {
        ArgumentNullException.ThrowIfNull(sink);
        string[] supported = NotificationTags.GetDeliveryTags(sink.DeliveryTags).ToArray();
        if (supported.Length == 0)
        {
            throw new InvalidOperationException(
                $"Notification sink '{sink.ProviderName}' must declare at least one delivery tag.");
        }

        return requestedDeliveryTags
            .Where(requested => supported.Contains(requested, StringComparer.Ordinal))
            .ToArray();
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
