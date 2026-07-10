namespace Gma.Framework.Tenancy.Messaging;

using Gma.Framework.Messaging;

public interface ITenantIntegrationEvent : IScopedIntegrationEvent
{
    string TenantId { get; }
}
