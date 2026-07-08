namespace Gma.Framework.Messaging;

public interface IIntegrationEventSubscriptionRegistry
{
    IReadOnlyCollection<IntegrationEventSubscription> Subscriptions { get; }
}
