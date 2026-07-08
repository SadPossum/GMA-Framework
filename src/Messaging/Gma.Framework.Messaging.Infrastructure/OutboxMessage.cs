namespace Gma.Framework.Messaging.Infrastructure;

using Gma.Framework.Runtime.Workers;
using Gma.Framework.Messaging;

public class OutboxMessage
{
    public const int SubjectMaxLength = OutboxMessageRecord.SubjectMaxLength;
    public const int EventTypeMaxLength = OutboxMessageRecord.EventTypeMaxLength;
    public const int LockedByMaxLength = WorkerIds.MaxLength;
    public const int ErrorMaxLength = 2048;
    public const string DefaultError = "Unknown error.";

    public Guid Id { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public string? ScopeId { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public string Payload { get; private set; } = string.Empty;
    public DateTimeOffset? ProcessedAtUtc { get; private set; }
    public DateTimeOffset? LockedUntilUtc { get; private set; }
    public string? LockedBy { get; private set; }
    public DateTimeOffset? NextAttemptAtUtc { get; private set; }
    public int Attempts { get; private set; }
    public string? Error { get; private set; }

    private OutboxMessage() { }

    public OutboxMessage(
        Guid id,
        string subject,
        string eventType,
        int version,
        string? scopeId,
        DateTimeOffset occurredAtUtc,
        string payload,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("id must not be empty.", nameof(id));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);

        this.Id = id;
        this.Subject = IntegrationEventNaming.NormalizeSubject(subject);
        if (this.Subject.Length > SubjectMaxLength)
        {
            throw new ArgumentException($"{nameof(subject)} must be {SubjectMaxLength} characters or fewer.", nameof(subject));
        }

        this.EventType = NormalizeRequired(eventType, EventTypeMaxLength, nameof(eventType));
        this.Version = version;
        this.ScopeId = MessageScopeIds.NormalizeOptional(scopeId, nameof(scopeId));
        this.OccurredAtUtc = RequireTimestamp(occurredAtUtc, nameof(occurredAtUtc));
        this.CreatedAtUtc = RequireTimestamp(createdAtUtc, nameof(createdAtUtc));
        this.Payload = NormalizeRequired(payload, int.MaxValue, nameof(payload));
    }

    public void MarkClaimed(string workerId, DateTimeOffset nowUtc, TimeSpan lockDuration)
    {
        if (this.ProcessedAtUtc is not null)
        {
            throw new InvalidOperationException("Processed outbox messages cannot be claimed.");
        }

        DateTimeOffset normalizedNowUtc = RequireTimestamp(nowUtc, nameof(nowUtc));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lockDuration, TimeSpan.Zero);

        if (this.LockedUntilUtc is not null && this.LockedUntilUtc > normalizedNowUtc)
        {
            throw new InvalidOperationException("Outbox message is already claimed.");
        }

        if (this.NextAttemptAtUtc is not null && this.NextAttemptAtUtc > normalizedNowUtc)
        {
            throw new InvalidOperationException("Outbox message cannot be claimed before its next retry is due.");
        }

        this.LockedBy = WorkerIds.Normalize(workerId);
        this.LockedUntilUtc = normalizedNowUtc.Add(lockDuration);
    }

    public void MarkProcessed(DateTimeOffset nowUtc)
    {
        if (this.ProcessedAtUtc is not null)
        {
            throw new InvalidOperationException("Outbox message is already processed.");
        }

        this.EnsureClaimed();

        this.ProcessedAtUtc = RequireTimestamp(nowUtc, nameof(nowUtc));
        this.LockedBy = null;
        this.LockedUntilUtc = null;
        this.NextAttemptAtUtc = null;
        this.Error = null;
    }

    public void MarkFailed(string error, DateTimeOffset nowUtc, int maxAttempts)
    {
        if (this.ProcessedAtUtc is not null)
        {
            throw new InvalidOperationException("Processed outbox messages cannot be marked as failed.");
        }

        this.EnsureClaimed();
        DateTimeOffset normalizedNowUtc = RequireTimestamp(nowUtc, nameof(nowUtc));
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);

        this.Attempts++;
        this.Error = NormalizeError(error);
        this.LockedBy = null;
        this.LockedUntilUtc = null;
        this.NextAttemptAtUtc = this.Attempts >= maxAttempts
            ? null
            : normalizedNowUtc.Add(GetRetryDelay(this.Attempts));
    }

    private void EnsureClaimed()
    {
        if (string.IsNullOrWhiteSpace(this.LockedBy) || this.LockedUntilUtc is null)
        {
            throw new InvalidOperationException("Outbox message must be claimed before changing publish state.");
        }
    }

    private static TimeSpan GetRetryDelay(int attempts)
    {
        int seconds = Math.Min(300, (int)Math.Pow(2, Math.Min(attempts, 8)));
        return TimeSpan.FromSeconds(seconds);
    }

    private static DateTimeOffset RequireTimestamp(DateTimeOffset value, string parameterName) =>
        value == default
            ? throw new ArgumentException($"{parameterName} must not be the default timestamp.", parameterName)
            : value;

    private static string NormalizeRequired(
        string value,
        int maxLength,
        string parameterName,
        bool lowerInvariant = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        string normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"{parameterName} must be {maxLength} characters or fewer.", parameterName);
        }

        return lowerInvariant ? normalized.ToLowerInvariant() : normalized;
    }

    private static string NormalizeError(string error)
    {
        string normalized = string.IsNullOrWhiteSpace(error)
            ? DefaultError
            : error.Trim();

        return normalized.Length > ErrorMaxLength
            ? normalized[..ErrorMaxLength]
            : normalized;
    }
}
