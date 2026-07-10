namespace Gma.Framework.Messaging.Infrastructure;

using Gma.Framework.Messaging;

internal sealed class ScopedIntegrationEventScopeResolver : IIntegrationEventScopeResolver
{
    public string? ResolveScopeId(IIntegrationEvent integrationEvent) =>
        integrationEvent is IScopedIntegrationEvent scopedIntegrationEvent
            ? scopedIntegrationEvent.ScopeId
            : null;
}
