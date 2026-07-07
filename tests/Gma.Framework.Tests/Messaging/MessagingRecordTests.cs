namespace Gma.Framework.Tests;

using Gma.Framework.Messaging;
using Xunit;

[Trait("Category", "Unit")]
public sealed class MessagingRecordTests
{
    private static readonly DateTimeOffset OccurredAtUtc = new(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid EventId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public void Integration_event_envelope_normalizes_message_metadata()
    {
        IntegrationEventEnvelope envelope = new(
            EventId,
            " GMA.Auth.Member-Registered.V1 ",
            " Gma.Modules.Auth.Contracts.MemberRegisteredIntegrationEvent ",
            1,
            " tenant-a ",
            OccurredAtUtc,
            " {} ");

        Assert.Equal("gma.auth.member-registered.v1", envelope.Subject);
        Assert.Equal("Gma.Modules.Auth.Contracts.MemberRegisteredIntegrationEvent", envelope.EventType);
        Assert.Equal("tenant-a", envelope.ScopeId);
        Assert.Equal("{}", envelope.Payload);
    }

    [Fact]
    public void Outbox_message_record_normalizes_message_metadata()
    {
        OutboxMessageRecord record = new(
            EventId,
            " GMA.Catalog.Item-Created.V1 ",
            " Catalog.Contracts.CatalogItemCreatedIntegrationEvent ",
            1,
            " tenant-a ",
            OccurredAtUtc,
            " {} ");

        Assert.Equal("gma.catalog.item-created.v1", record.Subject);
        Assert.Equal("Catalog.Contracts.CatalogItemCreatedIntegrationEvent", record.EventType);
        Assert.Equal("tenant-a", record.ScopeId);
        Assert.Equal("{}", record.Payload);
    }

    [Fact]
    public void Inbox_message_record_normalizes_message_metadata()
    {
        InboxMessageRecord record = new(
            EventId,
            " Catalog-Item-Created-Projection ",
            " GMA.Catalog.Item-Created.V1 ",
            " Item-Created ",
            1,
            " tenant-a ",
            OccurredAtUtc);

        Assert.Equal("catalog-item-created-projection", record.HandlerName);
        Assert.Equal("gma.catalog.item-created.v1", record.Subject);
        Assert.Equal("item-created", record.EventType);
        Assert.Equal("tenant-a", record.ScopeId);
    }

    [Fact]
    public void Message_records_reject_invalid_identity_and_timestamps()
    {
        Assert.Throws<ArgumentException>(() => CreateEnvelope(eventId: Guid.Empty));
        Assert.Throws<ArgumentException>(() => CreateOutboxRecord(id: Guid.Empty));
        Assert.Throws<ArgumentException>(() => CreateInboxRecord(eventId: Guid.Empty));
        Assert.Throws<ArgumentException>(() => CreateEnvelope(occurredAtUtc: default(DateTimeOffset)));
        Assert.Throws<ArgumentException>(() => CreateOutboxRecord(occurredAtUtc: default(DateTimeOffset)));
        Assert.Throws<ArgumentException>(() => CreateInboxRecord(occurredAtUtc: default(DateTimeOffset)));
    }

    [Fact]
    public void Message_records_reject_non_positive_versions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateEnvelope(version: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateOutboxRecord(version: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateInboxRecord(version: 0));
    }

    [Fact]
    public void Message_records_reject_invalid_subjects()
    {
        Assert.Throws<ArgumentException>(() => CreateEnvelope(subject: "auth.member-registered"));
        Assert.Throws<ArgumentException>(() => CreateOutboxRecord(subject: "auth.member-registered"));
        Assert.Throws<ArgumentException>(() => CreateInboxRecord(subject: "auth.member-registered"));
    }

    [Fact]
    public void Message_records_reject_invalid_scope_ids()
    {
        string scopeId = new('x', MessageScopeIds.MaxLength + 1);

        Assert.Throws<ArgumentException>(() => CreateEnvelope(scopeId: scopeId));
        Assert.Throws<ArgumentException>(() => CreateOutboxRecord(scopeId: scopeId));
        Assert.Throws<ArgumentException>(() => CreateInboxRecord(scopeId: scopeId));
    }

    [Fact]
    public void Message_records_reject_invalid_metadata_text()
    {
        Assert.Throws<ArgumentException>(() => CreateEnvelope(eventType: " "));
        Assert.Throws<ArgumentException>(() => CreateOutboxRecord(eventType: $"Event{char.MinValue}Type"));
        Assert.Throws<ArgumentException>(() => CreateInboxRecord(handlerName: "handler name"));
        Assert.Throws<ArgumentException>(() => CreateInboxRecord(eventType: "event name"));
        Assert.Throws<ArgumentException>(() => CreateEnvelope(payload: " "));
        Assert.Throws<ArgumentException>(() => CreateOutboxRecord(payload: " "));
    }

    private static IntegrationEventEnvelope CreateEnvelope(
        Guid? eventId = null,
        string subject = "gma.auth.member-registered.v1",
        string eventType = "Gma.Modules.Auth.Contracts.MemberRegisteredIntegrationEvent",
        int version = 1,
        string? scopeId = "tenant-a",
        DateTimeOffset? occurredAtUtc = null,
        string payload = "{}") =>
        new(
            eventId ?? EventId,
            subject,
            eventType,
            version,
            scopeId,
            occurredAtUtc ?? OccurredAtUtc,
            payload);

    private static OutboxMessageRecord CreateOutboxRecord(
        Guid? id = null,
        string subject = "gma.auth.member-registered.v1",
        string eventType = "Gma.Modules.Auth.Contracts.MemberRegisteredIntegrationEvent",
        int version = 1,
        string? scopeId = "tenant-a",
        DateTimeOffset? occurredAtUtc = null,
        string payload = "{}") =>
        new(
            id ?? EventId,
            subject,
            eventType,
            version,
            scopeId,
            occurredAtUtc ?? OccurredAtUtc,
            payload);

    private static InboxMessageRecord CreateInboxRecord(
        Guid? eventId = null,
        string handlerName = "member-registered-projection",
        string subject = "gma.auth.member-registered.v1",
        string eventType = "member-registered",
        int version = 1,
        string? scopeId = "tenant-a",
        DateTimeOffset? occurredAtUtc = null) =>
        new(
            eventId ?? EventId,
            handlerName,
            subject,
            eventType,
            version,
            scopeId,
            occurredAtUtc ?? OccurredAtUtc);
}
