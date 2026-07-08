namespace Gma.Framework.Tests;

using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Infrastructure;
using Xunit;

[Trait("Category", "Unit")]
public sealed class InboxMessageTests
{
    [Fact]
    public void Create_normalizes_and_validates_scope_id()
    {
        InboxMessage message = CreateMessage(" tenant-a ");

        Assert.Equal("tenant-a", message.ScopeId);
        Assert.Null(CreateMessage(null).ScopeId);
        Assert.Throws<ArgumentException>(() => CreateMessage(new string('x', MessageScopeIds.MaxLength + 1)));
    }

    [Fact]
    public void Create_validates_message_identity()
    {
        Assert.Throws<ArgumentException>(() => CreateMessageWithMetadata(id: Guid.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateMessageWithMetadata(version: 0));
        Assert.Throws<ArgumentException>(() => CreateMessageWithMetadata(occurredAtUtc: default(DateTimeOffset)));
        Assert.Throws<ArgumentException>(() => CreateMessageWithMetadata(createdAtUtc: default(DateTimeOffset)));
        Assert.Throws<ArgumentException>(() => CreateMessageWithMetadata(subject: "catalog.item-updated"));
        Assert.Throws<ArgumentException>(() => CreateMessageWithMetadata(eventType: "item updated"));
    }

    [Fact]
    public void Mark_processed_clears_processing_state()
    {
        InboxMessage message = CreateMessage();

        message.MarkProcessing("worker-1", DateTimeOffset.UtcNow);
        message.MarkProcessed(DateTimeOffset.UtcNow);

        Assert.True(message.IsProcessed);
        Assert.Null(message.LockedBy);
        Assert.Null(message.LastError);
        Assert.NotNull(message.ProcessedAtUtc);
    }

    [Fact]
    public void Mark_failed_records_bounded_error()
    {
        InboxMessage message = CreateMessage();

        message.MarkProcessing("worker-1", DateTimeOffset.UtcNow);
        message.MarkFailed(new string('x', 4096), DateTimeOffset.UtcNow);

        Assert.Equal(InboxMessageStatus.Failed, message.Status);
        Assert.Equal(InboxMessage.LastErrorMaxLength, message.LastError?.Length);
        Assert.Null(message.LockedBy);
    }

    [Fact]
    public void Mark_failed_normalizes_error()
    {
        InboxMessage message = CreateMessage();

        message.MarkProcessing("worker-1", DateTimeOffset.UtcNow);
        message.MarkFailed($" handler{char.MinValue}failed ", DateTimeOffset.UtcNow);

        Assert.Equal("handler failed", message.LastError);
        Assert.Equal(InboxProcessResult.ErrorMaxLength, InboxMessage.LastErrorMaxLength);
    }

    [Fact]
    public void Mark_processing_retries_failed_message_and_clears_failure_state()
    {
        DateTimeOffset now = new(2026, 7, 2, 9, 0, 0, TimeSpan.Zero);
        InboxMessage message = CreateMessage();

        message.MarkProcessing("worker-1", now);
        message.MarkFailed("handler failed", now.AddSeconds(1));
        message.MarkProcessing("worker-2", now.AddSeconds(2));

        Assert.Equal(InboxMessageStatus.Processing, message.Status);
        Assert.Equal(2, message.Attempts);
        Assert.Equal("worker-2", message.LockedBy);
        Assert.Null(message.FailedAtUtc);
        Assert.Null(message.LastError);
    }

    [Fact]
    public void Create_validates_mapped_metadata_lengths()
    {
        Assert.Throws<ArgumentException>(() => CreateMessageWithMetadata(handler: new string('x', InboxMessage.HandlerMaxLength + 1)));
        Assert.Throws<ArgumentException>(() => CreateMessageWithMetadata(subject: new string('x', InboxMessage.SubjectMaxLength + 1)));
        Assert.Throws<ArgumentException>(() => CreateMessageWithMetadata(eventType: new string('x', InboxMessage.EventTypeMaxLength + 1)));
    }

    [Fact]
    public void Mark_processing_validates_worker_id_length()
    {
        InboxMessage message = CreateMessage();

        Assert.Throws<ArgumentException>(() =>
            message.MarkProcessing(new string('x', InboxMessage.LockedByMaxLength + 1), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Mark_processing_rejects_default_timestamp_without_mutating_state()
    {
        InboxMessage message = CreateMessage();

        Assert.Throws<ArgumentException>(() => message.MarkProcessing("worker-1", default));

        Assert.Equal(InboxMessageStatus.Pending, message.Status);
        Assert.Equal(0, message.Attempts);
        Assert.Null(message.ProcessingStartedAtUtc);
        Assert.Null(message.LockedBy);
    }

    [Fact]
    public void Mark_processing_rejects_unknown_status()
    {
        InboxMessage message = (InboxMessage)Activator.CreateInstance(typeof(InboxMessage), nonPublic: true)!;

        Assert.Equal(InboxMessageStatus.Unknown, message.Status);
        Assert.Throws<InvalidOperationException>(() => message.MarkProcessing("worker-1", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Completion_transitions_reject_default_timestamps()
    {
        InboxMessage processed = CreateMessage();
        processed.MarkProcessing("worker-1", DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentException>(() => processed.MarkProcessed(default));
        Assert.Equal(InboxMessageStatus.Processing, processed.Status);
        Assert.Equal("worker-1", processed.LockedBy);

        InboxMessage failed = CreateMessage();
        failed.MarkProcessing("worker-1", DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentException>(() => failed.MarkFailed("handler failed", default));
        Assert.Equal(InboxMessageStatus.Processing, failed.Status);
        Assert.Equal("worker-1", failed.LockedBy);
    }

    [Fact]
    public void Completion_transitions_require_processing_message()
    {
        InboxMessage message = CreateMessage();

        Assert.Throws<InvalidOperationException>(() => message.MarkProcessed(DateTimeOffset.UtcNow));
        Assert.Throws<InvalidOperationException>(() => message.MarkFailed("handler failed", DateTimeOffset.UtcNow));

        message.MarkProcessing("worker-1", DateTimeOffset.UtcNow);
        message.MarkProcessed(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => message.MarkProcessed(DateTimeOffset.UtcNow));
        Assert.Throws<InvalidOperationException>(() => message.MarkFailed("handler failed", DateTimeOffset.UtcNow));
        Assert.Throws<InvalidOperationException>(() => message.MarkProcessing("worker-1", DateTimeOffset.UtcNow));
    }

    private static InboxMessage CreateMessage(string? scopeId = "tenant-a") =>
        CreateMessageWithMetadata(
            handler: "handler",
            subject: "gma.catalog.item-updated.v1",
            eventType: "item-updated",
            scopeId: scopeId);

    private static InboxMessage CreateMessageWithMetadata(
        Guid? id = null,
        string handler = "handler",
        string subject = "gma.catalog.item-updated.v1",
        string eventType = "item-updated",
        string? scopeId = "tenant-a",
        int version = 1,
        DateTimeOffset? occurredAtUtc = null,
        DateTimeOffset? createdAtUtc = null) =>
        InboxMessage.Create(
            id ?? Guid.NewGuid(),
            handler,
            subject,
            eventType,
            version,
            scopeId,
            occurredAtUtc ?? DateTimeOffset.UtcNow,
            createdAtUtc ?? DateTimeOffset.UtcNow);
}
