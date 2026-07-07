namespace Gma.Framework.Tenancy.Messaging.Infrastructure;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy.Messaging;

internal sealed class TenantIntegrationEventScopeResolver : IIntegrationEventScopeResolver
{
    public string? ResolveScopeId(IIntegrationEvent integrationEvent) =>
        integrationEvent is ITenantIntegrationEvent tenantIntegrationEvent
            ? tenantIntegrationEvent.TenantId
            : null;
}
