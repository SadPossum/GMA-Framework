namespace Gma.Framework.Messaging.Infrastructure;

using System.Data;
using Microsoft.EntityFrameworkCore;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Messaging;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Runtime.Workers;

public abstract class EfInboxStore<TDbContext>(
    TDbContext dbContext,
    ISystemClock clock,
    IIdGenerator idGenerator,
    string moduleName)
    : IInboxStore, IInboxCleanupStore
    where TDbContext : DbContext
{
    private const string HandlerCanceledError = "Handler execution was canceled before completion.";

    public string ModuleName { get; } = IntegrationEventNaming.NormalizeModuleName(moduleName);

    public async Task<InboxProcessResult> ProcessAsync(
        InboxMessageRecord message,
        Func<CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(handler);

        string workerId = WorkerIds.Create(Environment.MachineName, idGenerator.NewId());

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                .ConfigureAwait(false);

        InboxMessage? inboxMessage = await dbContext.Set<InboxMessage>()
            .SingleOrDefaultAsync(
                item => item.Id == message.EventId && item.Handler == message.HandlerName,
                cancellationToken)
            .ConfigureAwait(false);

        if (inboxMessage?.IsProcessed == true)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return InboxProcessResult.Duplicate();
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        if (inboxMessage is null)
        {
            inboxMessage = InboxMessage.Create(
                message.EventId,
                message.HandlerName,
                message.Subject,
                message.EventType,
                message.Version,
                message.ScopeId,
                message.OccurredAtUtc,
                nowUtc);
            dbContext.Set<InboxMessage>().Add(inboxMessage);
        }

        inboxMessage.MarkProcessing(workerId, nowUtc);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await InvokeHandlerAsync(handler, cancellationToken).ConfigureAwait(false);
            inboxMessage.MarkProcessed(clock.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return InboxProcessResult.Processed();
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            string error = GetFailureMessage(exception);
            await this.RecordFailureAsync(message, workerId, error).ConfigureAwait(false);
            return InboxProcessResult.Failed(error);
        }
    }

    public async Task<int> DeleteProcessedBeforeAsync(
        DateTimeOffset processedBeforeUtc,
        int maxMessages,
        CancellationToken cancellationToken)
    {
        if (processedBeforeUtc == default)
        {
            throw new ArgumentException(
                $"{nameof(processedBeforeUtc)} must not be the default timestamp.",
                nameof(processedBeforeUtc));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(maxMessages, 1);

        return await dbContext.Set<InboxMessage>()
            .Where(message =>
                message.Status == InboxMessageStatus.Processed &&
                message.ProcessedAtUtc != null &&
                message.ProcessedAtUtc < processedBeforeUtc)
            .OrderBy(message => message.ProcessedAtUtc)
            .Take(maxMessages)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task InvokeHandlerAsync(
        Func<CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        Task? handlerTask = handler(cancellationToken);

        if (handlerTask is null)
        {
            throw new InvalidOperationException("Inbox handler returned a null task.");
        }

        await handlerTask.ConfigureAwait(false);
    }

    private static string GetFailureMessage(Exception exception)
    {
        if (exception is OperationCanceledException)
        {
            return HandlerCanceledError;
        }

        return string.IsNullOrWhiteSpace(exception.Message)
            ? exception.GetType().Name
            : exception.Message;
    }

    private async Task RecordFailureAsync(
        InboxMessageRecord message,
        string workerId,
        string error)
    {
        dbContext.ChangeTracker.Clear();

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, CancellationToken.None)
                .ConfigureAwait(false);

        InboxMessage? inboxMessage = await dbContext.Set<InboxMessage>()
            .SingleOrDefaultAsync(
                item => item.Id == message.EventId && item.Handler == message.HandlerName,
                CancellationToken.None)
            .ConfigureAwait(false);

        if (inboxMessage?.IsProcessed == true)
        {
            await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
            return;
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        if (inboxMessage is null)
        {
            inboxMessage = InboxMessage.Create(
                message.EventId,
                message.HandlerName,
                message.Subject,
                message.EventType,
                message.Version,
                message.ScopeId,
                message.OccurredAtUtc,
                nowUtc);
            dbContext.Set<InboxMessage>().Add(inboxMessage);
        }

        inboxMessage.MarkProcessing(workerId, nowUtc);
        inboxMessage.MarkFailed(error, clock.UtcNow);
        await dbContext.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
