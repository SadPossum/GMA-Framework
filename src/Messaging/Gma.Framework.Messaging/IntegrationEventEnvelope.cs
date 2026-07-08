namespace Gma.Framework.Messaging;

public sealed record IntegrationEventEnvelope
{
    public const int SubjectMaxLength = 256;
    public const int EventTypeMaxLength = 512;

    public IntegrationEventEnvelope(
        Guid eventId,
        string subject,
        string eventType,
        int version,
        string? scopeId,
        DateTimeOffset occurredAtUtc,
        string payload)
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
        this.Subject = NormalizeSubject(subject);
        this.EventType = NormalizeRequired(eventType, EventTypeMaxLength, nameof(eventType));
        this.Version = version;
        this.ScopeId = MessageScopeIds.NormalizeOptional(scopeId, nameof(scopeId));
        this.OccurredAtUtc = occurredAtUtc;
        this.Payload = NormalizeRequired(payload, int.MaxValue, nameof(payload), allowControlCharacters: true);
    }

    public Guid EventId { get; }
    public string Subject { get; }
    public string EventType { get; }
    public int Version { get; }
    public string? ScopeId { get; }
    public DateTimeOffset OccurredAtUtc { get; }
    public string Payload { get; }

    internal static string NormalizeSubject(string subject)
    {
        string normalized = IntegrationEventNaming.NormalizeSubject(subject);
        return normalized.Length <= SubjectMaxLength
            ? normalized
            : throw new ArgumentException($"subject must be {SubjectMaxLength} characters or fewer.", nameof(subject));
    }

    internal static string NormalizeRequired(
        string value,
        int maxLength,
        string parameterName,
        bool allowControlCharacters = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        string normalized = value.Trim();
        if (normalized.Length > maxLength ||
            (!allowControlCharacters && normalized.Any(char.IsControl)))
        {
            throw new ArgumentException(
                $"{parameterName} must be {maxLength} characters or fewer and cannot contain control characters.",
                parameterName);
        }

        return normalized;
    }
}
