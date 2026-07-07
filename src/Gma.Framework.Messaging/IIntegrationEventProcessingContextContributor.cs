namespace Gma.Framework.Messaging;

public interface IIntegrationEventProcessingContextContributor
{
    void Prepare(IntegrationEventSubscription subscription, IIntegrationEvent integrationEvent);
}
