namespace Gma.Framework.Notifications;

using Gma.Framework.ModuleComposition;

public static class NotificationsCompositionFeatures
{
    public static readonly CompositionFeatureId Publisher = new("notifications.publisher");
    public static readonly CompositionFeatureId RequestQueue = new("notifications.request-queue");
    public static readonly CompositionFeatureId CqrsRequestFlush = new("notifications.cqrs-request-flush");
    public static readonly CompositionFeatureId LiveFeed = new("notifications.live-feed");
    public static readonly CompositionFeatureId ServerSentEvents = new("notifications.sse");
    public static readonly CompositionFeatureId SignalR = new("notifications.signalr");
    public static readonly CompositionFeatureId History = new("notifications.history");
    public static readonly CompositionFeatureId Broadcasts = new("notifications.broadcasts");

    public static ProvidedCompositionFeature PublisherProvided(string provider) =>
        new(Publisher, provider, "User notification publisher services are registered.");

    public static ProvidedCompositionFeature RequestQueueProvided(string provider) =>
        new(RequestQueue, provider, "Deferred user notification request queue services are registered.");

    public static ProvidedCompositionFeature CqrsRequestFlushProvided(string provider) =>
        new(CqrsRequestFlush, provider, "CQRS command pipeline flushes deferred notification requests after successful commits.");

    public static ProvidedCompositionFeature LiveFeedProvided(string provider) =>
        new(LiveFeed, provider, "In-process live notification feed services are registered and enabled.");

    public static ProvidedCompositionFeature ServerSentEventsProvided(string provider) =>
        new(ServerSentEvents, provider, "Server-sent events notification front door is registered and enabled.");

    public static ProvidedCompositionFeature SignalRProvided(string provider) =>
        new(SignalR, provider, "SignalR notification front door is registered and enabled.");

    public static ProvidedCompositionFeature HistoryProvided(string provider) =>
        new(History, provider, "Durable notification history module is selected.");

    public static ProvidedCompositionFeature BroadcastsProvided(string provider) =>
        new(Broadcasts, provider, "Durable notification broadcast module is selected.");

    public static RequiredCompositionFeature PublisherRequired(string owner, string? reason = null, bool optional = false) =>
        new(Publisher, owner, optional, reason);

    public static RequiredCompositionFeature RequestQueueRequired(string owner, string? reason = null, bool optional = false) =>
        new(RequestQueue, owner, optional, reason);

    public static RequiredCompositionFeature CqrsRequestFlushRequired(string owner, string? reason = null, bool optional = false) =>
        new(CqrsRequestFlush, owner, optional, reason);

    public static RequiredCompositionFeature LiveFeedRequired(string owner, string? reason = null, bool optional = false) =>
        new(LiveFeed, owner, optional, reason);

    public static RequiredCompositionFeature ServerSentEventsRequired(string owner, string? reason = null, bool optional = false) =>
        new(ServerSentEvents, owner, optional, reason);

    public static RequiredCompositionFeature SignalRRequired(string owner, string? reason = null, bool optional = false) =>
        new(SignalR, owner, optional, reason);

    public static RequiredCompositionFeature HistoryRequired(string owner, string? reason = null, bool optional = false) =>
        new(History, owner, optional, reason);

    public static RequiredCompositionFeature BroadcastsRequired(string owner, string? reason = null, bool optional = false) =>
        new(Broadcasts, owner, optional, reason);
}
