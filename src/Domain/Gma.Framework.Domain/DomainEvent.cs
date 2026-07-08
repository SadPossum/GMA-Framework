namespace Gma.Framework.Domain;

public abstract record DomainEvent : IDomainEvent
{
    protected DomainEvent(Guid eventId, DateTimeOffset occurredAtUtc)
    {
        this.EventId = DomainEventGuards.RequireId(eventId, nameof(eventId));
        this.OccurredAtUtc = DomainEventGuards.RequireOccurredAtUtc(occurredAtUtc, nameof(occurredAtUtc));
    }

    public Guid EventId { get; }
    public DateTimeOffset OccurredAtUtc { get; }
}
