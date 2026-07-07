namespace Gma.Framework.Tests;

using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Infrastructure;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OutboxMessageTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Mark_processed_clears_lock_retry_and_error_state()
    {
        OutboxMessage message = CreateMessage();
        message.MarkClaimed("worker-a", Now, TimeSpan.FromMinutes(1));
        message.MarkFailed("temporary", Now, maxAttempts: 3);
        message.MarkClaimed("worker-a", Now.AddSeconds(3), TimeSpan.FromMinutes(1));

        message.MarkProcessed(Now.AddSeconds(4));

        Assert.Equal(Now.AddSeconds(4), message.ProcessedAtUtc);
        Assert.Null(message.LockedBy);
        Assert.Null(message.LockedUntilUtc);
        Assert.Null(message.NextAttemptAtUtc);
        Assert.Null(message.Error);
    }

    [Fact]
    public void Constructor_normalizes_and_validates_scope_id()
    {
        OutboxMessage message = CreateMessage(" tenant-a ");

        Assert.Equal("tenant-a", message.ScopeId);
        Assert.Null(CreateMessage(null).ScopeId);
        Assert.Throws<ArgumentException>(() => CreateMessage(new string('x', MessageScopeIds.MaxLength + 1)));
    }

    [Fact]
    public void Constructor_validates_message_identity()
    {
        Assert.Throws<ArgumentException>(() => CreateMessageWithMetadata(id: Guid.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateMessageWithMetadata(version: 0));
        Assert.Throws<ArgumentException>(() => CreateMessageWithMetadata(occurredAtUtc: default(DateTimeOffset)));
        Assert.Throws<ArgumentException>(() => CreateMessageWithMetadata(createdAtUtc: default(DateTimeOffset)));
        Assert.Throws<ArgumentException>(() => CreateMessageWithMetadata(subject: "auth.test"));
    }

    [Fact]
    public void Mark_failed_schedules_exponential_retry()
    {
        OutboxMessage message = CreateMessage();
        message.MarkClaimed("worker-a", Now, TimeSpan.FromMinutes(1));

        message.MarkFailed("temporary", Now, maxAttempts: 3);

        Assert.Equal(1, message.Attempts);
        Assert.Equal(Now.AddSeconds(2), message.NextAttemptAtUtc);
        Assert.Null(message.LockedBy);
        Assert.Null(message.LockedUntilUtc);
    }

    [Fact]
    public void Mark_failed_stops_retry_after_max_attempts()
    {
        OutboxMessage message = CreateMessage();

        message.MarkClaimed("worker-a", Now, TimeSpan.FromMinutes(1));
        message.MarkFailed("first", Now, maxAttempts: 2);
        message.MarkClaimed("worker-a", Now.AddSeconds(2), TimeSpan.FromMinutes(1));
        message.MarkFailed("second", Now.AddSeconds(2), maxAttempts: 2);

        Assert.Equal(2, message.Attempts);
        Assert.Null(message.NextAttemptAtUtc);
    }

    [Fact]
    public void Constructor_validates_mapped_metadata_lengths()
    {
        Assert.Throws<ArgumentException>(() => CreateMessageWithMetadata(subject: new string('x', OutboxMessage.SubjectMaxLength + 1)));
        Assert.Throws<ArgumentException>(() => CreateMessageWithMetadata(eventType: new string('x', OutboxMessage.EventTypeMaxLength + 1)));
    }

    [Fact]
    public void Mark_claimed_validates_worker_and_lock()
    {
        OutboxMessage message = CreateMessage();

        Assert.Throws<ArgumentException>(() =>
            message.MarkClaimed("worker-a", default, TimeSpan.FromMinutes(1)));
        Assert.Throws<ArgumentException>(() =>
            message.MarkClaimed(new string('x', OutboxMessage.LockedByMaxLength + 1), Now, TimeSpan.FromMinutes(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            message.MarkClaimed("worker-a", Now, TimeSpan.Zero));
    }

    [Fact]
    public void Mark_claimed_rejects_active_lock_until_it_expires()
    {
        OutboxMessage message = CreateMessage();

        message.MarkClaimed("worker-a", Now, TimeSpan.FromSeconds(10));

        Assert.Throws<InvalidOperationException>(() =>
            message.MarkClaimed("worker-b", Now.AddSeconds(9), TimeSpan.FromSeconds(10)));

        message.MarkClaimed("worker-b", Now.AddSeconds(10), TimeSpan.FromSeconds(10));

        Assert.Equal("worker-b", message.LockedBy);
        Assert.Equal(Now.AddSeconds(20), message.LockedUntilUtc);
    }

    [Fact]
    public void Mark_failed_records_bounded_error()
    {
        OutboxMessage message = CreateMessage();
        message.MarkClaimed("worker-a", Now, TimeSpan.FromMinutes(1));

        message.MarkFailed(new string('x', OutboxMessage.ErrorMaxLength + 1), Now, maxAttempts: 3);

        Assert.Equal(OutboxMessage.ErrorMaxLength, message.Error?.Length);
    }

    [Fact]
    public void Mark_failed_uses_default_error_for_blank_reason()
    {
        OutboxMessage message = CreateMessage();
        message.MarkClaimed("worker-a", Now, TimeSpan.FromMinutes(1));

        message.MarkFailed(" ", Now, maxAttempts: 3);

        Assert.Equal(OutboxMessage.DefaultError, message.Error);
    }

    [Fact]
    public void Completion_transitions_reject_default_timestamps()
    {
        OutboxMessage processed = CreateMessage();
        processed.MarkClaimed("worker-a", Now, TimeSpan.FromMinutes(1));

        Assert.Throws<ArgumentException>(() => processed.MarkProcessed(default));

        OutboxMessage failed = CreateMessage();
        failed.MarkClaimed("worker-a", Now, TimeSpan.FromMinutes(1));

        Assert.Throws<ArgumentException>(() => failed.MarkFailed("temporary", default, maxAttempts: 3));
    }

    [Fact]
    public void Completion_transitions_require_claimed_unprocessed_message()
    {
        OutboxMessage message = CreateMessage();

        Assert.Throws<InvalidOperationException>(() => message.MarkProcessed(Now));
        Assert.Throws<InvalidOperationException>(() => message.MarkFailed("temporary", Now, maxAttempts: 3));

        message.MarkClaimed("worker-a", Now, TimeSpan.FromMinutes(1));
        message.MarkFailed("temporary", Now, maxAttempts: 3);
        Assert.Throws<InvalidOperationException>(() =>
            message.MarkClaimed("worker-a", Now.AddSeconds(1), TimeSpan.FromMinutes(1)));

        message.MarkClaimed("worker-a", Now.AddSeconds(2), TimeSpan.FromMinutes(1));
        message.MarkProcessed(Now.AddSeconds(3));

        Assert.Throws<InvalidOperationException>(() =>
            message.MarkClaimed("worker-a", Now.AddSeconds(4), TimeSpan.FromMinutes(1)));
        Assert.Throws<InvalidOperationException>(() => message.MarkProcessed(Now.AddSeconds(4)));
        Assert.Throws<InvalidOperationException>(() => message.MarkFailed("temporary", Now.AddSeconds(4), maxAttempts: 3));
    }

    private static OutboxMessage CreateMessage(string? scopeId = "tenant-a") =>
        CreateMessageWithMetadata(
            subject: "gma.auth.test.v1",
            eventType: "test",
            scopeId: scopeId);

    private static OutboxMessage CreateMessageWithMetadata(
        Guid? id = null,
        string subject = "gma.auth.test.v1",
        string eventType = "test",
        string? scopeId = "tenant-a",
        int version = 1,
        DateTimeOffset? occurredAtUtc = null,
        DateTimeOffset? createdAtUtc = null) =>
        new(
            id ?? Guid.NewGuid(),
            subject,
            eventType,
            version,
            scopeId,
            occurredAtUtc ?? Now,
            "{}",
            createdAtUtc ?? Now);
}
