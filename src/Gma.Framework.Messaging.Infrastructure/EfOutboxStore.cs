namespace Gma.Framework.Messaging.Infrastructure;

using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Gma.Framework.Messaging;

public abstract class EfOutboxStore<TDbContext>(
    TDbContext dbContext,
    IOptions<OutboxOptions> options,
    string moduleName)
    : IOutboxStore
    where TDbContext : DbContext
{
    public string ModuleName { get; } = IntegrationEventNaming.NormalizeModuleName(moduleName);

    public async Task<IReadOnlyList<OutboxMessageRecord>> ClaimPendingAsync(
        int batchSize,
        string workerId,
        DateTimeOffset nowUtc,
        TimeSpan lockDuration,
        CancellationToken cancellationToken)
    {
        var arguments = OutboxStoreGuards.ValidateClaimArguments(batchSize, workerId, nowUtc, lockDuration);

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);

        List<OutboxMessage> messages = await dbContext.Set<OutboxMessage>()
            .Where(message =>
                message.ProcessedAtUtc == null &&
                message.Attempts < options.Value.EffectiveMaxAttempts &&
                (message.NextAttemptAtUtc == null || message.NextAttemptAtUtc <= arguments.NowUtc) &&
                (message.LockedUntilUtc == null || message.LockedUntilUtc <= arguments.NowUtc))
            .OrderBy(message => message.CreatedAtUtc)
            .Take(arguments.BatchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (OutboxMessage message in messages)
        {
            message.MarkClaimed(arguments.WorkerId, arguments.NowUtc, arguments.LockDuration);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return messages.Select(ToRecord).ToList();
    }

    public async Task MarkProcessedAsync(
        Guid id,
        string workerId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var arguments = OutboxStoreGuards.ValidateMarkArguments(id, workerId, nowUtc);

        OutboxMessage? message = await dbContext.Set<OutboxMessage>()
            .FirstOrDefaultAsync(
                item => item.Id == arguments.Id && item.LockedBy == arguments.WorkerId,
                cancellationToken)
            .ConfigureAwait(false);

        if (message is null)
        {
            return;
        }

        message.MarkProcessed(arguments.NowUtc);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkFailedAsync(
        Guid id,
        string workerId,
        string error,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var arguments = OutboxStoreGuards.ValidateMarkArguments(id, workerId, nowUtc);

        OutboxMessage? message = await dbContext.Set<OutboxMessage>()
            .FirstOrDefaultAsync(
                item => item.Id == arguments.Id && item.LockedBy == arguments.WorkerId,
                cancellationToken)
            .ConfigureAwait(false);

        if (message is null)
        {
            return;
        }

        message.MarkFailed(error, arguments.NowUtc, options.Value.EffectiveMaxAttempts);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static OutboxMessageRecord ToRecord(OutboxMessage message) =>
        new(
            message.Id,
            message.Subject,
            message.EventType,
            message.Version,
            message.ScopeId,
            message.OccurredAtUtc,
            message.Payload);
}
