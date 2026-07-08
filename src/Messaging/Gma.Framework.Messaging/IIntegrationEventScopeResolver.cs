namespace Gma.Framework.Messaging;

public interface IIntegrationEventScopeResolver
{
    string? ResolveScopeId(IIntegrationEvent integrationEvent);
}
