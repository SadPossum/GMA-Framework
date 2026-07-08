namespace Gma.Framework.Messaging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Gma.Framework.Modules;

public static class IntegrationEventSubscriptionServiceCollectionExtensions
{
    public static IServiceCollection AddIntegrationEventHandler<TEvent, THandler>(
        this IServiceCollection services,
        string consumerModule,
        string producerModule)
        where TEvent : IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerModule);
        ArgumentException.ThrowIfNullOrWhiteSpace(producerModule);

        IntegrationEventMetadataReader.IntegrationEventMetadata publishedEvent =
            IntegrationEventMetadataReader.ReadRequired(typeof(TEvent));
        IntegrationEventHandlerAttribute handler = IntegrationEventHandlerAttribute.GetRequired(typeof(THandler));
        return services.AddIntegrationEventHandler<TEvent, THandler>(
            consumerModule,
            producerModule,
            publishedEvent.EventName,
            publishedEvent.Version,
            handler.HandlerName,
            publishedEvent.Metadata.Items);
    }

    public static IServiceCollection AddIntegrationEventHandler<TEvent, THandler>(
        this IServiceCollection services,
        string consumerModule,
        string subject,
        string handlerName,
        IReadOnlyList<ModuleMetadataItem>? metadata = null)
        where TEvent : IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        ArgumentNullException.ThrowIfNull(services);

        IntegrationEventSubscription subscription = IntegrationEventSubscription.Create<TEvent, THandler>(
            consumerModule,
            subject,
            handlerName,
            metadata);

        services.TryAddSingleton<IIntegrationEventSubscriptionRegistry, IntegrationEventSubscriptionRegistry>();
        services.TryAddScoped<THandler>();
        AddSubscription(services, subscription);

        return services;
    }

    public static IServiceCollection AddIntegrationEventHandler<TEvent, THandler>(
        this IServiceCollection services,
        string consumerModule,
        string producerModule,
        string eventName,
        int version,
        string handlerName,
        IReadOnlyList<ModuleMetadataItem>? metadata = null)
        where TEvent : IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        ArgumentNullException.ThrowIfNull(services);

        IntegrationEventSubscription subscription = IntegrationEventSubscription.Create<TEvent, THandler>(
            consumerModule,
            producerModule,
            eventName,
            version,
            handlerName,
            metadata);

        services.TryAddSingleton<IIntegrationEventSubscriptionRegistry, IntegrationEventSubscriptionRegistry>();
        services.TryAddScoped<THandler>();
        AddSubscription(services, subscription);

        return services;
    }

    private static void AddSubscription(IServiceCollection services, IntegrationEventSubscription subscription)
    {
        foreach (IntegrationEventSubscription existing in services
                     .Where(descriptor => descriptor.ServiceType == typeof(IntegrationEventSubscription))
                     .Select(descriptor => descriptor.ImplementationInstance)
                     .OfType<IntegrationEventSubscription>())
        {
            if (!string.Equals(existing.ConsumerModule, subscription.ConsumerModule, StringComparison.Ordinal) ||
                !string.Equals(existing.HandlerName, subscription.HandlerName, StringComparison.Ordinal))
            {
                continue;
            }

            if (IsSameSubscription(existing, subscription))
            {
                return;
            }

            throw new InvalidOperationException(
                $"Integration event handler '{subscription.ConsumerModule}.{subscription.HandlerName}' is already registered with different metadata.");
        }

        services.AddSingleton(subscription);
    }

    private static bool IsSameSubscription(
        IntegrationEventSubscription existing,
        IntegrationEventSubscription subscription) =>
        string.Equals(existing.Subject, subscription.Subject, StringComparison.Ordinal) &&
        existing.EventType == subscription.EventType &&
        existing.HandlerType == subscription.HandlerType &&
        existing.Metadata == subscription.Metadata;
}
