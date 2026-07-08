namespace Gma.Framework.Tenancy.Messaging;

using Gma.Framework.Messaging;

public interface ITenantIntegrationEvent : IIntegrationEvent
{
    string TenantId { get; }
}
