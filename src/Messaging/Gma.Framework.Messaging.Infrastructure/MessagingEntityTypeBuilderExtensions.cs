namespace Gma.Framework.Messaging.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Gma.Framework.Messaging;

public static class MessagingEntityTypeBuilderExtensions
{
    public static EntityTypeBuilder<OutboxMessage> ConfigureOutboxMessage(
        this EntityTypeBuilder<OutboxMessage> builder,
        string tableName = "outbox_messages")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        builder.ToTable(tableName);
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Subject).HasMaxLength(OutboxMessage.SubjectMaxLength).IsRequired();
        builder.Property(message => message.EventType).HasMaxLength(OutboxMessage.EventTypeMaxLength).IsRequired();
        builder.Property(message => message.ScopeId)
            .HasColumnName("TenantId")
            .HasMaxLength(MessageScopeIds.MaxLength);
        builder.Property(message => message.LockedBy).HasMaxLength(OutboxMessage.LockedByMaxLength);
        builder.Property(message => message.Payload).IsRequired();
        builder.HasIndex(message => new
        {
            message.ProcessedAtUtc,
            message.NextAttemptAtUtc,
            message.LockedUntilUtc,
            message.CreatedAtUtc
        });

        return builder;
    }

    public static EntityTypeBuilder<InboxMessage> ConfigureInboxMessage(
        this EntityTypeBuilder<InboxMessage> builder,
        string tableName = "inbox_messages")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        builder.ToTable(tableName);
        builder.HasKey(message => new { message.Id, message.Handler });
        builder.Property(message => message.Handler).HasMaxLength(InboxMessage.HandlerMaxLength).IsRequired();
        builder.Property(message => message.Subject).HasMaxLength(InboxMessage.SubjectMaxLength).IsRequired();
        builder.Property(message => message.EventType).HasMaxLength(InboxMessage.EventTypeMaxLength).IsRequired();
        builder.Property(message => message.ScopeId)
            .HasColumnName("TenantId")
            .HasMaxLength(MessageScopeIds.MaxLength);
        builder.Property(message => message.Status).HasConversion<int>().IsRequired();
        builder.Property(message => message.LockedBy).HasMaxLength(InboxMessage.LockedByMaxLength);
        builder.Property(message => message.LastError).HasMaxLength(InboxMessage.LastErrorMaxLength);
        builder.HasIndex(message => new { message.Handler, message.Status });

        return builder;
    }
}
