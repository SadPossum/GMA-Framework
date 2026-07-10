namespace Gma.Framework.Tenancy.Messaging.Infrastructure;

using Gma.Framework.Messaging;

internal sealed class TenantIntegrationEventScopeResolver : IIntegrationEventScopeResolver
{
    public string? ResolveScopeId(IIntegrationEvent integrationEvent) =>
        integrationEvent is IScopedIntegrationEvent scopedIntegrationEvent
            ? scopedIntegrationEvent.ScopeId
            : null;
}
