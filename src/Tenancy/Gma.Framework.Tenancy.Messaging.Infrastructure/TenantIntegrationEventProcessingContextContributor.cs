namespace Gma.Framework.Tenancy.Messaging.Infrastructure;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

internal sealed class TenantIntegrationEventProcessingContextContributor(
    ITenantContextAccessor tenantContext)
    : IIntegrationEventProcessingContextContributor
{
    public void Prepare(IntegrationEventSubscription subscription, IIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        ArgumentNullException.ThrowIfNull(integrationEvent);

        tenantContext.ClearTenant();
        if (!subscription.IsTenantScoped())
        {
            return;
        }

        if (integrationEvent is not ITenantIntegrationEvent tenantIntegrationEvent)
        {
            throw new InvalidOperationException(
                $"Tenant-scoped subscription '{subscription.ConsumerModule}.{subscription.HandlerName}' requires event '{subscription.EventType.FullName}' to implement {nameof(ITenantIntegrationEvent)}.");
        }

        tenantContext.SetTenant(tenantIntegrationEvent.TenantId);
    }
}
