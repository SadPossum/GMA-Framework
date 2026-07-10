namespace Gma.Framework.Tenancy.Messaging;

using Gma.Framework.Messaging;

public abstract record TenantIntegrationEvent : ScopedIntegrationEvent, ITenantIntegrationEvent
{
    protected TenantIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        string eventName,
        int version)
        : base(eventId, tenantId, occurredAtUtc, eventName, version)
    {
    }

    public string TenantId => this.ScopeId;
}
