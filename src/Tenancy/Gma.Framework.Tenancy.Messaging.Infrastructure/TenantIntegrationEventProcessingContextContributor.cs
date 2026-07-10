namespace Gma.Framework.Tenancy.Messaging.Infrastructure;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;

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

        if (integrationEvent is not IScopedIntegrationEvent scopedIntegrationEvent)
        {
            throw new InvalidOperationException(
                $"Scope-aware subscription '{subscription.ConsumerModule}.{subscription.HandlerName}' requires event '{subscription.EventType.FullName}' to implement {nameof(IScopedIntegrationEvent)}.");
        }

        tenantContext.SetTenant(scopedIntegrationEvent.ScopeId);
    }
}
