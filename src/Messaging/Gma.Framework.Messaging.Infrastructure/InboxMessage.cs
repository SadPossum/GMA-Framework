namespace Gma.Framework.Messaging.Infrastructure;

using Gma.Framework.Runtime.Workers;
using Gma.Framework.Messaging;

public class InboxMessage
{
    public const int HandlerMaxLength = InboxMessageRecord.HandlerNameMaxLength;
    public const int SubjectMaxLength = InboxMessageRecord.SubjectMaxLength;
    public const int EventTypeMaxLength = InboxMessageRecord.EventTypeMaxLength;
    public const int LockedByMaxLength = WorkerIds.MaxLength;
    public const int LastErrorMaxLength = InboxProcessResult.ErrorMaxLength;

    public Guid Id { get; private set; }
    public string Handler { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public string? ScopeId { get; private set; }
    public InboxMessageStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ProcessingStartedAtUtc { get; private set; }
    public DateTimeOffset? ProcessedAtUtc { get; private set; }
    public DateTimeOffset? FailedAtUtc { get; private set; }
    public string? LockedBy { get; private set; }
    public string? LastError { get; private set; }

    private InboxMessage() { }

    private InboxMessage(
        Guid id,
        string handler,
        string subject,
        string eventType,
        int version,
        string? scopeId,
        DateTimeOffset occurredAtUtc,
        DateTimeOffset createdAtUtc)
    {
        this.Id = id;
        this.Handler = handler;
        this.Subject = subject;
        this.EventType = eventType;
        this.Version = version;
        this.ScopeId = scopeId;
        this.OccurredAtUtc = occurredAtUtc;
        this.CreatedAtUtc = createdAtUtc;
        this.Status = InboxMessageStatus.Pending;
    }

    public static InboxMessage Create(
        Guid id,
        string handler,
        string subject,
        string eventType,
        int version,
        string? scopeId,
        DateTimeOffset occurredAtUtc,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("id must not be empty.", nameof(id));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        DateTimeOffset normalizedOccurredAtUtc = RequireTimestamp(occurredAtUtc, nameof(occurredAtUtc));
        DateTimeOffset normalizedCreatedAtUtc = RequireTimestamp(createdAtUtc, nameof(createdAtUtc));

        string? normalizedScopeId = MessageScopeIds.NormalizeOptional(scopeId, nameof(scopeId));

        return new InboxMessage(
            id,
            NormalizeHandler(handler),
            NormalizeSubject(subject),
            NormalizeEventType(eventType),
            version,
            normalizedScopeId,
            normalizedOccurredAtUtc,
            normalizedCreatedAtUtc);
    }

    public bool IsProcessed => this.Status == InboxMessageStatus.Processed;

    public void MarkProcessing(string workerId, DateTimeOffset nowUtc)
    {
        if (this.Status == InboxMessageStatus.Unknown)
        {
            throw new InvalidOperationException("Inbox message status is unknown.");
        }

        if (this.Status == InboxMessageStatus.Processed)
        {
            throw new InvalidOperationException("Processed inbox messages cannot be processed again.");
        }

        DateTimeOffset normalizedNowUtc = RequireTimestamp(nowUtc, nameof(nowUtc));
        this.Status = InboxMessageStatus.Processing;
        this.Attempts++;
        this.ProcessingStartedAtUtc = normalizedNowUtc;
        this.LockedBy = WorkerIds.Normalize(workerId);
        this.FailedAtUtc = null;
        this.LastError = null;
    }

    public void MarkProcessed(DateTimeOffset nowUtc)
    {
        this.EnsureProcessing();
        DateTimeOffset normalizedNowUtc = RequireTimestamp(nowUtc, nameof(nowUtc));
        this.Status = InboxMessageStatus.Processed;
        this.ProcessedAtUtc = normalizedNowUtc;
        this.FailedAtUtc = null;
        this.LockedBy = null;
        this.LastError = null;
    }

    public void MarkFailed(string error, DateTimeOffset nowUtc)
    {
        this.EnsureProcessing();
        DateTimeOffset normalizedNowUtc = RequireTimestamp(nowUtc, nameof(nowUtc));
        this.Status = InboxMessageStatus.Failed;
        this.FailedAtUtc = normalizedNowUtc;
        this.LockedBy = null;
        this.LastError = InboxProcessResult.NormalizeError(error);
    }

    private void EnsureProcessing()
    {
        if (this.Status != InboxMessageStatus.Processing ||
            string.IsNullOrWhiteSpace(this.LockedBy) ||
            this.ProcessingStartedAtUtc is null)
        {
            throw new InvalidOperationException("Inbox message must be processing before it can be completed.");
        }
    }

    private static string NormalizeHandler(string handler)
    {
        string normalized = IntegrationEventNaming.NormalizeHandlerName(handler, nameof(handler));
        return normalized.Length <= HandlerMaxLength
            ? normalized
            : throw new ArgumentException($"{nameof(handler)} must be {HandlerMaxLength} characters or fewer.", nameof(handler));
    }

    private static string NormalizeSubject(string subject)
    {
        string normalized = IntegrationEventNaming.NormalizeSubject(subject, nameof(subject));
        return normalized.Length <= SubjectMaxLength
            ? normalized
            : throw new ArgumentException($"{nameof(subject)} must be {SubjectMaxLength} characters or fewer.", nameof(subject));
    }

    private static string NormalizeEventType(string eventType)
    {
        string normalized = IntegrationEventNaming.NormalizeEventName(eventType, nameof(eventType));
        return normalized.Length <= EventTypeMaxLength
            ? normalized
            : throw new ArgumentException($"{nameof(eventType)} must be {EventTypeMaxLength} characters or fewer.", nameof(eventType));
    }

    private static DateTimeOffset RequireTimestamp(DateTimeOffset value, string parameterName) =>
        value == default
            ? throw new ArgumentException($"{parameterName} must not be the default timestamp.", parameterName)
            : value;

}
