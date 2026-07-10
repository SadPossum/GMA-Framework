namespace Gma.Framework.Tests;

using Microsoft.EntityFrameworkCore;
using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Infrastructure;
using Xunit;

[Trait("Category", "Unit")]
public sealed class MessagingEntityTypeBuilderExtensionsTests
{
    [Fact]
    public void Configure_outbox_message_applies_common_table_key_lengths_and_index()
    {
        using TestMessagingDbContext dbContext = CreateDbContext();
        Microsoft.EntityFrameworkCore.Metadata.IEntityType entityType =
            dbContext.Model.FindEntityType(typeof(OutboxMessage)) ??
            throw new InvalidOperationException("Outbox message entity was not configured.");

        Assert.Equal("outbox_messages", entityType.GetTableName());
        Assert.Equal([nameof(OutboxMessage.Id)], entityType.FindPrimaryKey()?.Properties.Select(property => property.Name));
        Assert.Equal(
            OutboxMessage.SubjectMaxLength,
            entityType.FindProperty(nameof(OutboxMessage.Subject))?.GetMaxLength());
        Assert.Equal(
            OutboxMessage.EventTypeMaxLength,
            entityType.FindProperty(nameof(OutboxMessage.EventType))?.GetMaxLength());
        Assert.Equal(
            MessageScopeIds.MaxLength,
            entityType.FindProperty(nameof(OutboxMessage.ScopeId))?.GetMaxLength());
        Assert.Equal(
            "ScopeId",
            entityType.FindProperty(nameof(OutboxMessage.ScopeId))?.GetColumnName());
        Assert.Equal(
            OutboxMessage.LockedByMaxLength,
            entityType.FindProperty(nameof(OutboxMessage.LockedBy))?.GetMaxLength());
        Assert.Contains(
            entityType.GetIndexes(),
            index => index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(OutboxMessage.ProcessedAtUtc),
                nameof(OutboxMessage.NextAttemptAtUtc),
                nameof(OutboxMessage.LockedUntilUtc),
                nameof(OutboxMessage.CreatedAtUtc)
            ]));
    }

    [Fact]
    public void Configure_inbox_message_applies_common_table_key_lengths_and_index()
    {
        using TestMessagingDbContext dbContext = CreateDbContext();
        Microsoft.EntityFrameworkCore.Metadata.IEntityType entityType =
            dbContext.Model.FindEntityType(typeof(InboxMessage)) ??
            throw new InvalidOperationException("Inbox message entity was not configured.");

        Assert.Equal("inbox_messages", entityType.GetTableName());
        Assert.Equal(
            [nameof(InboxMessage.Id), nameof(InboxMessage.Handler)],
            entityType.FindPrimaryKey()?.Properties.Select(property => property.Name));
        Assert.Equal(
            InboxMessage.HandlerMaxLength,
            entityType.FindProperty(nameof(InboxMessage.Handler))?.GetMaxLength());
        Assert.Equal(
            InboxMessage.SubjectMaxLength,
            entityType.FindProperty(nameof(InboxMessage.Subject))?.GetMaxLength());
        Assert.Equal(
            InboxMessage.EventTypeMaxLength,
            entityType.FindProperty(nameof(InboxMessage.EventType))?.GetMaxLength());
        Assert.Equal(
            MessageScopeIds.MaxLength,
            entityType.FindProperty(nameof(InboxMessage.ScopeId))?.GetMaxLength());
        Assert.Equal(
            "ScopeId",
            entityType.FindProperty(nameof(InboxMessage.ScopeId))?.GetColumnName());
        Assert.Equal(
            InboxMessage.LockedByMaxLength,
            entityType.FindProperty(nameof(InboxMessage.LockedBy))?.GetMaxLength());
        Assert.Equal(
            InboxMessage.LastErrorMaxLength,
            entityType.FindProperty(nameof(InboxMessage.LastError))?.GetMaxLength());
        Assert.Contains(
            entityType.GetIndexes(),
            index => index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(InboxMessage.Handler),
                nameof(InboxMessage.Status)
            ]));
    }

    private static TestMessagingDbContext CreateDbContext()
    {
        DbContextOptions<TestMessagingDbContext> options =
            new DbContextOptionsBuilder<TestMessagingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;

        return new TestMessagingDbContext(options);
    }

    private sealed class TestMessagingDbContext(DbContextOptions<TestMessagingDbContext> options) : DbContext(options)
    {
        public DbSet<InboxMessage> InboxMessages => this.Set<InboxMessage>();
        public DbSet<OutboxMessage> OutboxMessages => this.Set<OutboxMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<InboxMessage>().ConfigureInboxMessage();
            modelBuilder.Entity<OutboxMessage>().ConfigureOutboxMessage();
        }
    }
}
