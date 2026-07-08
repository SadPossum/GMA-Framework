namespace Gma.Framework.Messaging;

public sealed record InboxMessageRecord
{
    public const int HandlerNameMaxLength = 256;
    public const int SubjectMaxLength = IntegrationEventEnvelope.SubjectMaxLength;
    public const int EventTypeMaxLength = 256;

    public InboxMessageRecord(
        Guid eventId,
        string handlerName,
        string subject,
        string eventType,
        int version,
        string? scopeId,
        DateTimeOffset occurredAtUtc)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("eventId must not be empty.", nameof(eventId));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);

        if (occurredAtUtc == default)
        {
            throw new ArgumentException("occurredAtUtc must not be the default timestamp.", nameof(occurredAtUtc));
        }

        this.EventId = eventId;
        this.HandlerName = NormalizeHandlerName(handlerName);
        this.Subject = IntegrationEventEnvelope.NormalizeSubject(subject);
        this.EventType = NormalizeEventType(eventType);
        this.Version = version;
        this.ScopeId = MessageScopeIds.NormalizeOptional(scopeId, nameof(scopeId));
        this.OccurredAtUtc = occurredAtUtc;
    }

    public Guid EventId { get; }
    public string HandlerName { get; }
    public string Subject { get; }
    public string EventType { get; }
    public int Version { get; }
    public string? ScopeId { get; }
    public DateTimeOffset OccurredAtUtc { get; }

    private static string NormalizeHandlerName(string handlerName)
    {
        string normalized = IntegrationEventNaming.NormalizeHandlerName(handlerName);
        return normalized.Length <= HandlerNameMaxLength
            ? normalized
            : throw new ArgumentException($"handlerName must be {HandlerNameMaxLength} characters or fewer.", nameof(handlerName));
    }

    private static string NormalizeEventType(string eventType)
    {
        string normalized = IntegrationEventNaming.NormalizeEventName(eventType);
        return normalized.Length <= EventTypeMaxLength
            ? normalized
            : throw new ArgumentException($"eventType must be {EventTypeMaxLength} characters or fewer.", nameof(eventType));
    }
}
