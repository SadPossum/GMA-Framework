namespace Gma.Framework.Tasks.Infrastructure;

using System.Data;
using Microsoft.EntityFrameworkCore;
using Gma.Framework.Tasks;
using Gma.Framework.Runtime.Time;

public abstract class EfTaskRunStore<TDbContext>(TDbContext dbContext, ISystemClock clock) : ITaskRunStore
    where TDbContext : DbContext
{
    public async Task EnqueueAsync(TaskRunRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        IQueryable<TaskRun> existingQuery = dbContext.Set<TaskRun>()
            .Where(taskRun => taskRun.Id == request.RunId);
        if (request.DeduplicationKey is not null)
        {
            existingQuery = existingQuery.Concat(dbContext.Set<TaskRun>().Where(taskRun =>
                taskRun.ModuleName == request.ModuleName &&
                taskRun.TaskName == request.TaskName &&
                taskRun.ScopeId == request.ScopeId &&
                taskRun.DeduplicationKey == request.DeduplicationKey &&
                taskRun.Status != TaskRunStatus.Succeeded &&
                taskRun.Status != TaskRunStatus.Failed &&
                taskRun.Status != TaskRunStatus.Canceled &&
                taskRun.Status != TaskRunStatus.TimedOut));
        }

        bool exists = await existingQuery
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);
        if (exists)
        {
            return;
        }

        dbContext.Set<TaskRun>().Add(TaskRun.Enqueue(request));
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TaskRunSummary>> ListAsync(
        TaskRunFilter filter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        IQueryable<TaskRun> query = ApplyFilter(dbContext.Set<TaskRun>().AsNoTracking(), filter);

        return await query
            .OrderByDescending(taskRun => taskRun.CreatedAtUtc)
            .ThenBy(taskRun => taskRun.Id)
            .Skip(filter.SkipCount)
            .Take(filter.PageSize)
            .Select(taskRun => ToSummary(taskRun))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<TaskRunDetails?> GetAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("Task run id must not be empty.", nameof(runId));
        }

        return await dbContext.Set<TaskRun>()
            .AsNoTracking()
            .Where(taskRun => taskRun.Id == runId)
            .Select(taskRun => new TaskRunDetails(
                ToSummary(taskRun),
                taskRun.Payload,
                taskRun.NodeId,
                taskRun.LeasedAtUtc,
                taskRun.NextAttemptAtUtc,
                taskRun.CancellationRequestedAtUtc,
                taskRun.CancellationRequestedBy))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<TaskRunStats> GetStatsAsync(
        TaskRunStatsFilter filter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        IQueryable<TaskRun> query = ApplyStatsFilter(dbContext.Set<TaskRun>().AsNoTracking(), filter);

        var rows = await query
            .GroupBy(taskRun => taskRun.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .OrderBy(item => item.Status)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        return new TaskRunStats(rows
            .Select(item => new TaskRunStatusCount(item.Status, item.Count))
            .ToArray());
    }

    public async Task<IReadOnlyList<TaskRunLease>> ClaimReadyAsync(
        TaskWorkerClaim claim,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                .ConfigureAwait(false);

        List<TaskRun> candidates = await dbContext.Set<TaskRun>()
            .Where(taskRun =>
                taskRun.WorkerGroup == claim.WorkerGroup &&
                taskRun.ScheduledAtUtc <= claim.ClaimedAtUtc &&
                taskRun.Attempts < taskRun.MaxAttempts &&
                (taskRun.NextAttemptAtUtc == null || taskRun.NextAttemptAtUtc <= claim.ClaimedAtUtc) &&
                (taskRun.LockedUntilUtc == null || taskRun.LockedUntilUtc <= claim.ClaimedAtUtc) &&
                (taskRun.Status == TaskRunStatus.Queued ||
                 taskRun.Status == TaskRunStatus.Leased ||
                 taskRun.Status == TaskRunStatus.Running ||
                 taskRun.Status == TaskRunStatus.CancellationRequested ||
                 taskRun.Status == TaskRunStatus.RetryScheduled))
            .OrderBy(taskRun => taskRun.ScheduledAtUtc)
            .ThenBy(taskRun => taskRun.CreatedAtUtc)
            .Take(claim.MaxRuns)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<TaskRunLease> leases = [];
        foreach (TaskRun taskRun in candidates.Where(taskRun => taskRun.CanClaim(claim.ClaimedAtUtc)))
        {
            leases.Add(taskRun.Claim(claim));
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return leases;
    }

    public async Task MarkStartedAsync(
        TaskExecutionContext context,
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken)
    {
        TaskRun? taskRun = await this.FindOwnedRunAsync(context, cancellationToken).ConfigureAwait(false);
        if (taskRun is null)
        {
            return;
        }

        taskRun.MarkStarted(context, startedAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkSucceededAsync(
        TaskExecutionContext context,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken)
    {
        TaskRun? taskRun = await this.FindOwnedRunAsync(context, cancellationToken).ConfigureAwait(false);
        if (taskRun is null)
        {
            return;
        }

        taskRun.MarkSucceeded(context, completedAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkCanceledAsync(
        TaskExecutionContext context,
        DateTimeOffset canceledAtUtc,
        CancellationToken cancellationToken)
    {
        TaskRun? taskRun = await this.FindOwnedRunAsync(context, cancellationToken).ConfigureAwait(false);
        if (taskRun is null)
        {
            return;
        }

        taskRun.MarkCanceled(context, canceledAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkFailedAsync(
        TaskExecutionContext context,
        string error,
        DateTimeOffset failedAtUtc,
        DateTimeOffset? retryAtUtc,
        CancellationToken cancellationToken)
    {
        TaskRun? taskRun = await this.FindOwnedRunAsync(context, cancellationToken).ConfigureAwait(false);
        if (taskRun is null)
        {
            return;
        }

        taskRun.MarkFailed(context, error, failedAtUtc, retryAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ReportHeartbeatAsync(
        TaskExecutionContext context,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        TaskRun? taskRun = await this.FindOwnedRunAsync(context, cancellationToken).ConfigureAwait(false);
        if (taskRun is null)
        {
            return;
        }

        taskRun.MarkHeartbeat(context, observedAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ReportProgressAsync(
        TaskExecutionContext context,
        TaskProgress progress,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        TaskRun? taskRun = await this.FindOwnedRunAsync(context, cancellationToken).ConfigureAwait(false);
        if (taskRun is null)
        {
            return;
        }

        taskRun.MarkProgress(context, progress, observedAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RequestCancellationAsync(
        Guid runId,
        string? requestedBy,
        DateTimeOffset requestedAtUtc,
        CancellationToken cancellationToken)
    {
        TaskRun? taskRun = await dbContext.Set<TaskRun>()
            .FirstOrDefaultAsync(item => item.Id == runId, cancellationToken)
            .ConfigureAwait(false);
        if (taskRun is null)
        {
            return;
        }

        taskRun.RequestCancellation(requestedBy, requestedAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RetryAsync(
        Guid runId,
        string? requestedBy,
        DateTimeOffset scheduledAtUtc,
        CancellationToken cancellationToken)
    {
        TaskRun? taskRun = await dbContext.Set<TaskRun>()
            .FirstOrDefaultAsync(item => item.Id == runId, cancellationToken)
            .ConfigureAwait(false);
        if (taskRun is null)
        {
            return;
        }

        taskRun.Retry(requestedBy, scheduledAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TaskRunSummary>> MarkStaleTimedOutAsync(
        DateTimeOffset nowUtc,
        TimeSpan staleAfter,
        int maxRuns,
        CancellationToken cancellationToken)
    {
        TaskRun.RequireTimestamp(nowUtc, nameof(nowUtc));
        if (staleAfter <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(staleAfter), staleAfter, "Stale timeout window must be positive.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(maxRuns, 1);
        DateTimeOffset staleBeforeUtc = nowUtc.Subtract(staleAfter);

        List<TaskRun> staleRuns = await dbContext.Set<TaskRun>()
            .Where(taskRun =>
                (taskRun.Status == TaskRunStatus.Leased && taskRun.LockedUntilUtc <= nowUtc) ||
                ((taskRun.Status == TaskRunStatus.Running || taskRun.Status == TaskRunStatus.CancellationRequested) &&
                 ((taskRun.LastHeartbeatAtUtc != null && taskRun.LastHeartbeatAtUtc <= staleBeforeUtc) ||
                  (taskRun.LastHeartbeatAtUtc == null && taskRun.LockedUntilUtc <= nowUtc))))
            .OrderBy(taskRun => taskRun.LockedUntilUtc)
            .ThenBy(taskRun => taskRun.StartedAtUtc)
            .Take(maxRuns)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (TaskRun taskRun in staleRuns)
        {
            taskRun.MarkTimedOut(nowUtc);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return staleRuns.Select(ToSummary).ToArray();
    }

    public async Task EnqueueControlMessageAsync(
        TaskControlMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        bool exists = await dbContext.Set<TaskControlMessageState>()
            .AnyAsync(item => item.Id == message.MessageId, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
        {
            return;
        }

        dbContext.Set<TaskControlMessageState>().Add(TaskControlMessageState.Enqueue(message));
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<TaskControlMessage>> ReadPendingAsync(
        TaskExecutionContext context,
        int maxMessages,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxMessages, 1);

        return this.ReadPendingCoreAsync(context, maxMessages, cancellationToken);
    }

    public async Task MarkHandledAsync(
        TaskExecutionContext context,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        TaskControlMessageState? message = await this.FindOwnedControlMessageAsync(
                context,
                messageId,
                cancellationToken)
            .ConfigureAwait(false);
        if (message is null)
        {
            return;
        }

        message.MarkHandled(clock.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkFailedAsync(
        TaskExecutionContext context,
        Guid messageId,
        string error,
        CancellationToken cancellationToken)
    {
        TaskControlMessageState? message = await this.FindOwnedControlMessageAsync(
                context,
                messageId,
                cancellationToken)
            .ConfigureAwait(false);
        if (message is null)
        {
            return;
        }

        message.MarkFailed(error, clock.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<TaskControlMessage>> ReadPendingCoreAsync(
        TaskExecutionContext context,
        int maxMessages,
        CancellationToken cancellationToken)
    {
        TaskRun? taskRun = await this.FindOwnedRunAsync(context, cancellationToken).ConfigureAwait(false);
        if (taskRun is null)
        {
            return [];
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        List<TaskControlMessageState> messages = await dbContext.Set<TaskControlMessageState>()
            .Where(message =>
                message.RunId == context.RunId &&
                (message.Status == TaskControlMessageStatus.Pending ||
                 message.Status == TaskControlMessageStatus.Delivered ||
                 message.Status == TaskControlMessageStatus.Failed) &&
                (message.ExpiresAtUtc == null || message.ExpiresAtUtc > nowUtc))
            .OrderBy(message => message.EnqueuedAtUtc)
            .Take(maxMessages)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (TaskControlMessageState message in messages)
        {
            message.MarkDelivered(nowUtc);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return messages
            .Select(message => new TaskControlMessage(
                message.Id,
                message.RunId,
                message.CommandName,
                message.Payload,
                message.EnqueuedAtUtc,
                message.RequestedBy,
                message.ExpiresAtUtc))
            .ToArray();
    }

    private static IQueryable<TaskRun> ApplyFilter(IQueryable<TaskRun> query, TaskRunFilter filter)
    {
        if (filter.ModuleName is not null)
        {
            query = query.Where(taskRun => taskRun.ModuleName == filter.ModuleName);
        }

        if (filter.TaskName is not null)
        {
            query = query.Where(taskRun => taskRun.TaskName == filter.TaskName);
        }

        if (filter.WorkerGroup is not null)
        {
            query = query.Where(taskRun => taskRun.WorkerGroup == filter.WorkerGroup);
        }

        if (filter.Status is not null)
        {
            query = query.Where(taskRun => taskRun.Status == filter.Status);
        }

        if (filter.ScopeId is not null)
        {
            query = query.Where(taskRun => taskRun.ScopeId == filter.ScopeId);
        }

        return query;
    }

    private static IQueryable<TaskRun> ApplyStatsFilter(IQueryable<TaskRun> query, TaskRunStatsFilter filter)
    {
        if (filter.ModuleName is not null)
        {
            query = query.Where(taskRun => taskRun.ModuleName == filter.ModuleName);
        }

        if (filter.TaskName is not null)
        {
            query = query.Where(taskRun => taskRun.TaskName == filter.TaskName);
        }

        if (filter.WorkerGroup is not null)
        {
            query = query.Where(taskRun => taskRun.WorkerGroup == filter.WorkerGroup);
        }

        if (filter.ScopeId is not null)
        {
            query = query.Where(taskRun => taskRun.ScopeId == filter.ScopeId);
        }

        return query;
    }

    private static TaskRunSummary ToSummary(TaskRun taskRun) =>
        new(
            taskRun.Id,
            taskRun.ModuleName,
            taskRun.TaskName,
            taskRun.WorkerGroup,
            taskRun.PayloadVersion,
            taskRun.Status,
            taskRun.ScopeId,
            taskRun.CorrelationId,
            taskRun.CreatedAtUtc,
            taskRun.ScheduledAtUtc,
            taskRun.StartedAtUtc,
            taskRun.CompletedAtUtc,
            taskRun.Attempts,
            taskRun.MaxAttempts,
            taskRun.LockedBy,
            taskRun.LockedUntilUtc,
            taskRun.LastHeartbeatAtUtc,
            taskRun.ProgressPercent,
            taskRun.ProgressMessage,
            taskRun.LastError,
            taskRun.RequestedBy,
            taskRun.DeduplicationKey);

    private async Task<TaskControlMessageState?> FindOwnedControlMessageAsync(
        TaskExecutionContext context,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        TaskRun? taskRun = await this.FindOwnedRunAsync(context, cancellationToken).ConfigureAwait(false);
        if (taskRun is null)
        {
            return null;
        }

        return await dbContext.Set<TaskControlMessageState>()
            .FirstOrDefaultAsync(
                message => message.Id == messageId && message.RunId == context.RunId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private Task<TaskRun?> FindOwnedRunAsync(
        TaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return dbContext.Set<TaskRun>()
            .FirstOrDefaultAsync(
                taskRun =>
                    taskRun.Id == context.RunId &&
                    taskRun.ModuleName == context.ModuleName &&
                    taskRun.TaskName == context.TaskName &&
                    taskRun.WorkerGroup == context.WorkerGroup &&
                    taskRun.LockedBy == context.WorkerId &&
                    taskRun.NodeId == context.NodeId,
                cancellationToken);
    }
}
