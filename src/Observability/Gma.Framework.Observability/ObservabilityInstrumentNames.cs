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
    public const string InboxMessages = ApplicationNamespaces.Default + ".inbox.messages";
    public const string InboxProcessDuration = ApplicationNamespaces.Default + ".inbox.process.duration";

    public const string CacheRequests = ApplicationNamespaces.Default + ".cache.requests";
    public const string CacheDuration = ApplicationNamespaces.Default + ".cache.duration";
    public const string CacheBackendFailures = ApplicationNamespaces.Default + ".cache.backend.failures";
    public const string CacheInvalidationFailures = ApplicationNamespaces.Default + ".cache.invalidation.failures";

    public const string NotificationsPublished = ApplicationNamespaces.Default + ".notifications.published";
    public const string NotificationsDelivered = ApplicationNamespaces.Default + ".notifications.delivered";
    public const string NotificationsDeliveryDuration = ApplicationNamespaces.Default + ".notifications.delivery.duration";

    public const string TaskClaimed = ApplicationNamespaces.Default + ".tasks.claimed";
    public const string TaskCompleted = ApplicationNamespaces.Default + ".tasks.completed";
    public const string TaskDuration = ApplicationNamespaces.Default + ".tasks.duration";
    public const string TaskTimedOut = ApplicationNamespaces.Default + ".tasks.timed_out";
    public const string TaskQueueDepth = ApplicationNamespaces.Default + ".tasks.queue.depth";
    public const string TaskActiveRuns = ApplicationNamespaces.Default + ".tasks.active.runs";

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

    public static string InboxMessagesFor(string applicationNamespace) =>
        Create(applicationNamespace, "inbox.messages");

    public static string InboxProcessDurationFor(string applicationNamespace) =>
        Create(applicationNamespace, "inbox.process.duration");

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

    private static string Create(string applicationNamespace, string instrumentName) =>
        $"{ApplicationNamespaces.Normalize(applicationNamespace)}.{instrumentName}";
}
