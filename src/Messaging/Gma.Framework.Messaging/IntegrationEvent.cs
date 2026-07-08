namespace Gma.Framework.Messaging;

public abstract record IntegrationEvent : IIntegrationEvent
{
    protected IntegrationEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        string eventName,
        int version)
    {
        this.EventId = IntegrationEventContractGuards.RequireId(eventId, nameof(eventId));
        this.OccurredAtUtc = IntegrationEventContractGuards.RequireOccurredAtUtc(
            occurredAtUtc,
            nameof(occurredAtUtc));
        this.EventName = IntegrationEventContractGuards.NormalizeEventName(eventName, nameof(eventName));
        this.Version = IntegrationEventContractGuards.RequireVersion(version, nameof(version));
    }

    public Guid EventId { get; }
    public DateTimeOffset OccurredAtUtc { get; }
    public string EventName { get; }
    public int Version { get; }
}
