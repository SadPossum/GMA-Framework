namespace Gma.Framework.Messaging;

using Gma.Framework.Modules;

public static class ModuleDescriptorMessagingExtensions
{
    public static ModuleDescriptorBuilder WithPublishedEvent<TEvent>(this ModuleDescriptorBuilder builder)
        where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(builder);
        IntegrationEventMetadataReader.IntegrationEventMetadata metadata =
            IntegrationEventMetadataReader.ReadRequired(typeof(TEvent));
        string subject = IntegrationEventNaming.CreateSubject(
            IntegrationEventNaming.DefaultSubjectPrefix,
            builder.Name,
            metadata.EventName,
            metadata.Version);

        return builder.WithPublishedEvent(new ModuleIntegrationEventDescriptor(
            metadata.EventName,
            subject,
            metadata.Version,
            metadata.Metadata.Items));
    }

    public static ModuleDescriptorBuilder WithPublishedEvent(
        this ModuleDescriptorBuilder builder,
        ModuleIntegrationEventDescriptor publishedEvent)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(publishedEvent);
        return builder.WithPublishedEvents([publishedEvent]);
    }

    public static ModuleDescriptorBuilder WithPublishedEvents(
        this ModuleDescriptorBuilder builder,
        IReadOnlyList<ModuleIntegrationEventDescriptor> publishedEvents)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithFeature(
            new ModulePublishedEventsDescriptor(publishedEvents),
            static (existing, incoming) =>
            {
                return new ModulePublishedEventsDescriptor(existing
                    .PublishedEvents
                    .Concat(incoming.PublishedEvents)
                    .ToArray());
            });
    }

    public static ModuleDescriptorBuilder WithSubscription(
        this ModuleDescriptorBuilder builder,
        ModuleSubscriptionDescriptor subscription)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(subscription);
        return builder.WithSubscriptions([subscription]);
    }

    public static ModuleDescriptorBuilder WithSubscription<TEvent>(
        this ModuleDescriptorBuilder builder,
        string producerModule,
        string handlerName,
        IReadOnlyList<ModuleMetadataItem>? metadata = null)
        where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(producerModule);
        ArgumentException.ThrowIfNullOrWhiteSpace(handlerName);

        IntegrationEventMetadataReader.IntegrationEventMetadata publishedEvent =
            IntegrationEventMetadataReader.ReadRequired(typeof(TEvent));
        ModuleMetadataItems subscriptionMetadata = ModuleMetadataItems.Create(metadata ?? publishedEvent.Metadata.Items);
        string normalizedProducerModule = IntegrationEventNaming.NormalizeModuleName(producerModule, nameof(producerModule));
        string subject = IntegrationEventNaming.CreateSubject(
            IntegrationEventNaming.DefaultSubjectPrefix,
            normalizedProducerModule,
            publishedEvent.EventName,
            publishedEvent.Version);

        return builder.WithSubscription(new ModuleSubscriptionDescriptor(
            normalizedProducerModule,
            publishedEvent.EventName,
            subject,
            handlerName,
            subscriptionMetadata.Items));
    }

    public static ModuleDescriptorBuilder WithSubscriptions(
        this ModuleDescriptorBuilder builder,
        IReadOnlyList<ModuleSubscriptionDescriptor> subscriptions)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithFeature(
            new ModuleSubscriptionsDescriptor(subscriptions),
            static (existing, incoming) =>
            {
                return new ModuleSubscriptionsDescriptor(existing
                    .Subscriptions
                    .Concat(incoming.Subscriptions)
                    .ToArray());
            });
    }

    public static IReadOnlyList<ModuleIntegrationEventDescriptor> GetPublishedEvents(this ModuleDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return descriptor.GetFeature<ModulePublishedEventsDescriptor>()?.PublishedEvents ?? [];
    }

    public static IReadOnlyList<ModuleSubscriptionDescriptor> GetSubscriptions(this ModuleDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return descriptor.GetFeature<ModuleSubscriptionsDescriptor>()?.Subscriptions ?? [];
    }
}
