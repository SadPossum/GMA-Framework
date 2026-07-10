namespace Gma.Framework.Domain;

public abstract record ScopedDomainEvent : DomainEvent
{
    protected ScopedDomainEvent(Guid eventId, DateTimeOffset occurredAtUtc, string scopeId)
        : base(eventId, occurredAtUtc)
        => this.ScopeId = DomainEventGuards.NormalizeScopeId(scopeId, nameof(scopeId));

    public string ScopeId { get; }
}
