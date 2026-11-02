namespace Gma.Framework.Observability;

using Gma.Framework.Naming;

public static class ObservabilityInstrumentNames
{
    public const string CommandsExecuted = ApplicationNamespaces.Default + ".commands.executed";
    public const string CommandsDuration = ApplicationNamespaces.Default + ".commands.duration";
    public const string QueriesExecuted = ApplicationNamespaces.Default + ".queries.executed";
    public const string QueriesDuration = ApplicationNamespaces.Default + ".queries.duration";

    public const string OutboxClaimed = ApplicationNamespaces.Default + ".outbox.claimed";
    public const string OutboxPublished = ApplicationNamespaces.Default + ".outbox.published";
    public const string OutboxFailed = ApplicationNamespaces.Default + ".outbox.failed";
    public const string OutboxPublishDuration = ApplicationNamespaces.Default + ".outbox.publish.duration";
    public const string OutboxBacklog = ApplicationNamespaces.Default + ".outbox.backlog";
    public const string OutboxExhausted = ApplicationNamespaces.Default + ".outbox.exhausted";
    public const string OutboxOldestPendingAge = ApplicationNamespaces.Default + ".outbox.oldest_pending.age";
    public const string InboxMessages = ApplicationNamespaces.Default + ".inbox.messages";
    public const string InboxProcessDuration = ApplicationNamespaces.Default + ".inbox.process.duration";
    public const string MessageJournalDeleted = ApplicationNamespaces.Default + ".message_journal.deleted";
    public const string MessageJournalCleanupFailures = ApplicationNamespaces.Default + ".message_journal.cleanup.failures";
    public const string MessageJournalCleanupDuration = ApplicationNamespaces.Default + ".message_journal.cleanup.duration";
    public const string MessageJournalOldestProcessedAge = ApplicationNamespaces.Default + ".message_journal.oldest_processed.age";

    public const string CacheRequests = ApplicationNamespaces.Default + ".cache.requests";
    public const string CacheDuration = ApplicationNamespaces.Default + ".cache.duration";
    public const string CacheBackendFailures = ApplicationNamespaces.Default + ".cache.backend.failures";
    public const string CacheInvalidationFailures = ApplicationNamespaces.Default + ".cache.invalidation.failures";

    public const string NotificationsPublished = ApplicationNamespaces.Default + ".notifications.published";
    public const string NotificationsDelivered = ApplicationNamespaces.Default + ".notifications.delivered";
    public const string NotificationsDeliveryDuration = ApplicationNamespaces.Default + ".notifications.delivery.duration";
    public const string NotificationsDurableDeliveryAttempts = ApplicationNamespaces.Default + ".notifications.durable_delivery.attempts";
    public const string NotificationsDurableDeliveryDuration = ApplicationNamespaces.Default + ".notifications.durable_delivery.duration";
    public const string NotificationsDurableDeliveryBacklog = ApplicationNamespaces.Default + ".notifications.durable_delivery.backlog";
    public const string NotificationsDurableDeliveryExhausted = ApplicationNamespaces.Default + ".notifications.durable_delivery.exhausted";
    public const string NotificationsDurableDeliveryOldestPendingAge = ApplicationNamespaces.Default + ".notifications.durable_delivery.oldest_pending.age";

    public const string TaskClaimed = ApplicationNamespaces.Default + ".tasks.claimed";
    public const string TaskCompleted = ApplicationNamespaces.Default + ".tasks.completed";
    public const string TaskDuration = ApplicationNamespaces.Default + ".tasks.duration";
    public const string TaskTimedOut = ApplicationNamespaces.Default + ".tasks.timed_out";
    public const string TaskQueueDepth = ApplicationNamespaces.Default + ".tasks.queue.depth";
    public const string TaskActiveRuns = ApplicationNamespaces.Default + ".tasks.active.runs";
    public const string TaskRetentionDeleted = ApplicationNamespaces.Default + ".tasks.retention.deleted";
    public const string TaskRetentionFailures = ApplicationNamespaces.Default + ".tasks.retention.failures";
    public const string TaskRetentionDuration = ApplicationNamespaces.Default + ".tasks.retention.duration";
    public const string TaskRetentionOldestTerminalAge = ApplicationNamespaces.Default + ".tasks.retention.oldest_terminal.age";
    public const string SecuritySignals = ApplicationNamespaces.Default + ".security.signals";

    public static string CommandsExecutedFor(string applicationNamespace) =>
        Create(applicationNamespace, "commands.executed");

    public static string CommandsDurationFor(string applicationNamespace) =>
        Create(applicationNamespace, "commands.duration");

    public static string QueriesExecutedFor(string applicationNamespace) =>
        Create(applicationNamespace, "queries.executed");

    public static string QueriesDurationFor(string applicationNamespace) =>
        Create(applicationNamespace, "queries.duration");

    public static string OutboxClaimedFor(string applicationNamespace) =>
        Create(applicationNamespace, "outbox.claimed");

    public static string OutboxPublishedFor(string applicationNamespace) =>
        Create(applicationNamespace, "outbox.published");

    public static string OutboxFailedFor(string applicationNamespace) =>
        Create(applicationNamespace, "outbox.failed");

    public static string OutboxPublishDurationFor(string applicationNamespace) =>
        Create(applicationNamespace, "outbox.publish.duration");

    public static string OutboxBacklogFor(string applicationNamespace) =>
        Create(applicationNamespace, "outbox.backlog");

    public static string OutboxExhaustedFor(string applicationNamespace) =>
        Create(applicationNamespace, "outbox.exhausted");

    public static string OutboxOldestPendingAgeFor(string applicationNamespace) =>
        Create(applicationNamespace, "outbox.oldest_pending.age");

    public static string InboxMessagesFor(string applicationNamespace) =>
        Create(applicationNamespace, "inbox.messages");

    public static string InboxProcessDurationFor(string applicationNamespace) =>
        Create(applicationNamespace, "inbox.process.duration");

    public static string MessageJournalDeletedFor(string applicationNamespace) =>
        Create(applicationNamespace, "message_journal.deleted");

    public static string MessageJournalCleanupFailuresFor(string applicationNamespace) =>
        Create(applicationNamespace, "message_journal.cleanup.failures");

    public static string MessageJournalCleanupDurationFor(string applicationNamespace) =>
        Create(applicationNamespace, "message_journal.cleanup.duration");

    public static string MessageJournalOldestProcessedAgeFor(string applicationNamespace) =>
        Create(applicationNamespace, "message_journal.oldest_processed.age");

    public static string CacheRequestsFor(string applicationNamespace) =>
        Create(applicationNamespace, "cache.requests");

    public static string CacheDurationFor(string applicationNamespace) =>
        Create(applicationNamespace, "cache.duration");

    public static string CacheBackendFailuresFor(string applicationNamespace) =>
        Create(applicationNamespace, "cache.backend.failures");

    public static string CacheInvalidationFailuresFor(string applicationNamespace) =>
        Create(applicationNamespace, "cache.invalidation.failures");

    public static string NotificationsPublishedFor(string applicationNamespace) =>
        Create(applicationNamespace, "notifications.published");

    public static string NotificationsDeliveredFor(string applicationNamespace) =>
        Create(applicationNamespace, "notifications.delivered");

    public static string NotificationsDeliveryDurationFor(string applicationNamespace) =>
        Create(applicationNamespace, "notifications.delivery.duration");

    public static string NotificationsDurableDeliveryAttemptsFor(string applicationNamespace) =>
        Create(applicationNamespace, "notifications.durable_delivery.attempts");

    public static string NotificationsDurableDeliveryDurationFor(string applicationNamespace) =>
        Create(applicationNamespace, "notifications.durable_delivery.duration");

    public static string NotificationsDurableDeliveryBacklogFor(string applicationNamespace) =>
        Create(applicationNamespace, "notifications.durable_delivery.backlog");

    public static string NotificationsDurableDeliveryExhaustedFor(string applicationNamespace) =>
        Create(applicationNamespace, "notifications.durable_delivery.exhausted");

    public static string NotificationsDurableDeliveryOldestPendingAgeFor(string applicationNamespace) =>
        Create(applicationNamespace, "notifications.durable_delivery.oldest_pending.age");

    public static string TaskClaimedFor(string applicationNamespace) =>
        Create(applicationNamespace, "tasks.claimed");

    public static string TaskCompletedFor(string applicationNamespace) =>
        Create(applicationNamespace, "tasks.completed");

    public static string TaskDurationFor(string applicationNamespace) =>
        Create(applicationNamespace, "tasks.duration");

    public static string TaskTimedOutFor(string applicationNamespace) =>
        Create(applicationNamespace, "tasks.timed_out");

    public static string TaskQueueDepthFor(string applicationNamespace) =>
        Create(applicationNamespace, "tasks.queue.depth");

    public static string TaskActiveRunsFor(string applicationNamespace) =>
        Create(applicationNamespace, "tasks.active.runs");

    public static string TaskRetentionDeletedFor(string applicationNamespace) =>
        Create(applicationNamespace, "tasks.retention.deleted");

    public static string TaskRetentionFailuresFor(string applicationNamespace) =>
        Create(applicationNamespace, "tasks.retention.failures");

    public static string TaskRetentionDurationFor(string applicationNamespace) =>
        Create(applicationNamespace, "tasks.retention.duration");

    public static string TaskRetentionOldestTerminalAgeFor(string applicationNamespace) =>
        Create(applicationNamespace, "tasks.retention.oldest_terminal.age");

    public static string SecuritySignalsFor(string applicationNamespace) =>
        Create(applicationNamespace, "security.signals");

    private static string Create(string applicationNamespace, string instrumentName) =>
        $"{ApplicationNamespaces.Normalize(applicationNamespace)}.{instrumentName}";
}
