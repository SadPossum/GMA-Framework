namespace Gma.Framework.Domain;

public abstract record TenantDomainEvent : DomainEvent
{
    protected TenantDomainEvent(Guid eventId, DateTimeOffset occurredAtUtc, string tenantId)
        : base(eventId, occurredAtUtc)
        => this.TenantId = DomainEventGuards.NormalizeTenantId(tenantId, nameof(tenantId));

    public string TenantId { get; }
}
