namespace Gma.Framework.Messaging;

using Gma.Framework.ModuleComposition;

public static class MessagingCompositionFeatures
{
    public static readonly CompositionFeatureId Outbox = new("messaging.outbox");
    public static readonly CompositionFeatureId Inbox = new("messaging.inbox");
    public static readonly CompositionFeatureId OutboxPublishing = new("messaging.outbox-publishing");
    public static readonly CompositionFeatureId EventBus = new("messaging.event-bus");
    public static readonly CompositionFeatureId NatsPublishing = new("messaging.nats-publishing");
    public static readonly CompositionFeatureId NatsConsumers = new("messaging.nats-consumers");

    public static ProvidedCompositionFeature OutboxProvided(string provider) =>
        new(Outbox, provider, "Outbox writer registry and outbox runtime contracts are registered.");

    public static ProvidedCompositionFeature InboxProvided(string provider) =>
        new(Inbox, provider, "Inbox processing contracts and metrics are registered.");

    public static ProvidedCompositionFeature OutboxPublishingProvided(string provider) =>
        new(OutboxPublishing, provider, "Hosted outbox publishing runtime is registered.");

    public static ProvidedCompositionFeature EventBusProvided(string provider) =>
        new(EventBus, provider, "A concrete integration event bus is registered.");

    public static ProvidedCompositionFeature NatsPublishingProvided(string provider) =>
        new(NatsPublishing, provider, "NATS JetStream publishing adapter is registered.");

    public static ProvidedCompositionFeature NatsConsumersProvided(string provider) =>
        new(NatsConsumers, provider, "NATS JetStream consumer hosted service is registered and enabled.");

    public static RequiredCompositionFeature OutboxRequired(string owner, string? reason = null, bool optional = false) =>
        new(Outbox, owner, optional, reason);

    public static RequiredCompositionFeature InboxRequired(string owner, string? reason = null, bool optional = false) =>
        new(Inbox, owner, optional, reason);

    public static RequiredCompositionFeature OutboxPublishingRequired(string owner, string? reason = null, bool optional = false) =>
        new(OutboxPublishing, owner, optional, reason);

    public static RequiredCompositionFeature EventBusRequired(string owner, string? reason = null, bool optional = false) =>
        new(EventBus, owner, optional, reason);

    public static RequiredCompositionFeature NatsPublishingRequired(string owner, string? reason = null, bool optional = false) =>
        new(NatsPublishing, owner, optional, reason);

    public static RequiredCompositionFeature NatsConsumersRequired(string owner, string? reason = null, bool optional = false) =>
        new(NatsConsumers, owner, optional, reason);
}
