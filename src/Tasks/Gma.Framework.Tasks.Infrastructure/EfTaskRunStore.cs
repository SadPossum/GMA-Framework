namespace Gma.Framework.Tasks.Infrastructure;

using System.Data;
using Microsoft.EntityFrameworkCore;
using Gma.Framework.Tasks;
using Gma.Framework.Runtime.Time;

public abstract class EfTaskRunStore<TDbContext>(TDbContext dbContext, ISystemClock clock) : ITaskRunStore
    where TDbContext : DbContext
{
    protected TDbContext StoreDbContext => dbContext;

    protected ISystemClock StoreClock => clock;

    public virtual async Task<TaskRunEnqueueResult> EnqueueAsync(
        TaskRunRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        TaskRun? existing = await this.FindCanonicalRunAsync(request, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return new TaskRunEnqueueResult(ToDetails(existing), Created: false);
        }

        TaskRun taskRun = TaskRun.Enqueue(request);
        dbContext.Set<TaskRun>().Add(taskRun);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new TaskRunEnqueueResult(ToDetails(taskRun), Created: true);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(taskRun).State = EntityState.Detached;
            existing = await this.FindCanonicalRunAsync(request, cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                throw;
            }

            return new TaskRunEnqueueResult(ToDetails(existing), Created: false);
        }
    }

    public async Task<TaskRunPage> ListAsync(
        TaskRunFilter filter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        IQueryable<TaskRun> query = ApplyFilter(dbContext.Set<TaskRun>().AsNoTracking(), filter);

        int totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        TaskRunSummary[] items = await query
            .OrderByDescending(taskRun => taskRun.CreatedAtUtc)
            .ThenBy(taskRun => taskRun.Id)
            .Skip(filter.SkipCount)
            .Take(filter.PageSize)
            .Select(taskRun => ToSummary(taskRun))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        return new TaskRunPage(
            items,
            totalCount,
            (filter.SkipCount / filter.PageSize) + 1,
            filter.PageSize);
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
            .Select(taskRun => ToDetails(taskRun))
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

    public virtual Task<IReadOnlyList<TaskRunLease>> ClaimReadyAsync(
        TaskWorkerClaim claim,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);

        return this.ClaimLockedAsync(
            claim,
            IsolationLevel.Serializable,
            token => dbContext.Set<TaskRun>()
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
                .ThenBy(taskRun => taskRun.Id)
                .Take(claim.MaxRuns)
                .ToListAsync(token),
            cancellationToken);
    }

    protected async Task<IReadOnlyList<TaskRunLease>> ClaimLockedAsync(
        TaskWorkerClaim claim,
        IsolationLevel isolationLevel,
        Func<CancellationToken, Task<List<TaskRun>>> loadCandidates,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(loadCandidates);

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(isolationLevel, cancellationToken)
                .ConfigureAwait(false);

        List<TaskRun> candidates = await loadCandidates(cancellationToken).ConfigureAwait(false);

        List<TaskRunLease> leases = [];
        foreach (TaskRun taskRun in candidates.Where(taskRun => taskRun.CanClaim(claim.ClaimedAtUtc)))
        {
            leases.Add(taskRun.Claim(claim));
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return leases;
    }

    public Task<TaskRunMutationOutcome> MarkStartedAsync(
        TaskExecutionContext context,
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken)
        => this.UpdateOwnedRunAsync(
            context,
            taskRun => TaskRunStatusTransitions.CanStart(taskRun.Status),
            taskRun => taskRun.MarkStarted(context, startedAtUtc),
            cancellationToken);

    public Task<TaskRunMutationOutcome> MarkSucceededAsync(
        TaskExecutionContext context,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken)
        => this.UpdateOwnedRunAsync(
            context,
            taskRun => TaskRunStatusTransitions.CanComplete(taskRun.Status),
            taskRun => taskRun.MarkSucceeded(context, completedAtUtc),
            cancellationToken);

    public Task<TaskRunMutationOutcome> MarkCanceledAsync(
        TaskExecutionContext context,
        DateTimeOffset canceledAtUtc,
        CancellationToken cancellationToken)
        => this.UpdateOwnedRunAsync(
            context,
            taskRun => taskRun.Status is TaskRunStatus.Leased or TaskRunStatus.Running or TaskRunStatus.CancellationRequested,
            taskRun => taskRun.MarkCanceled(context, canceledAtUtc),
            cancellationToken);

    public Task<TaskRunMutationOutcome> MarkFailedAsync(
        TaskExecutionContext context,
        string error,
        DateTimeOffset failedAtUtc,
        DateTimeOffset? retryAtUtc,
        CancellationToken cancellationToken)
        => this.UpdateOwnedRunAsync(
            context,
            taskRun => TaskRunStatusTransitions.CanComplete(taskRun.Status) || taskRun.Status == TaskRunStatus.Leased,
            taskRun => taskRun.MarkFailed(context, error, failedAtUtc, retryAtUtc),
            cancellationToken);

    public Task<TaskRunMutationOutcome> ReportHeartbeatAsync(
        TaskExecutionContext context,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
        => this.UpdateOwnedRunAsync(
            context,
            taskRun => taskRun.Status is TaskRunStatus.Running or TaskRunStatus.CancellationRequested,
            taskRun => taskRun.MarkHeartbeat(context, observedAtUtc),
            cancellationToken);

    public Task<TaskRunMutationOutcome> ReportProgressAsync(
        TaskExecutionContext context,
        TaskProgress progress,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
        => this.UpdateOwnedRunAsync(
            context,
            taskRun => taskRun.Status is TaskRunStatus.Running or TaskRunStatus.CancellationRequested,
            taskRun => taskRun.MarkProgress(context, progress, observedAtUtc),
            cancellationToken);

    public async Task<TaskRunMutationOutcome> RequestCancellationAsync(
        Guid runId,
        string? requestedBy,
        DateTimeOffset requestedAtUtc,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            TaskRun? taskRun = await dbContext.Set<TaskRun>()
                .FirstOrDefaultAsync(item => item.Id == runId, cancellationToken)
                .ConfigureAwait(false);
            if (taskRun is null)
            {
                return TaskRunMutationOutcome.NotFound;
            }

            if (taskRun.Status is TaskRunStatus.Canceled or TaskRunStatus.CancellationRequested)
            {
                return TaskRunMutationOutcome.AlreadyApplied;
            }

            if (!TaskRunStatusTransitions.CanRequestCancellation(taskRun.Status))
            {
                return TaskRunMutationOutcome.InvalidState;
            }

            try
            {
                _ = taskRun.RequestCancellation(requestedBy, requestedAtUtc);
            }
            catch (ArgumentException)
            {
                return TaskRunMutationOutcome.InvalidRequest;
            }
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return TaskRunMutationOutcome.Applied;
            }
            catch (DbUpdateConcurrencyException) when (attempt < 2)
            {
                dbContext.ChangeTracker.Clear();
            }
        }

        return TaskRunMutationOutcome.Conflict;
    }

    public async Task<TaskRunMutationOutcome> RetryAsync(
        Guid runId,
        string? requestedBy,
        DateTimeOffset scheduledAtUtc,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            TaskRun? taskRun = await dbContext.Set<TaskRun>()
                .FirstOrDefaultAsync(item => item.Id == runId, cancellationToken)
                .ConfigureAwait(false);
            if (taskRun is null)
            {
                return TaskRunMutationOutcome.NotFound;
            }

            if (taskRun.Status is not (TaskRunStatus.Failed or TaskRunStatus.TimedOut or TaskRunStatus.Canceled or TaskRunStatus.RetryScheduled))
            {
                return TaskRunMutationOutcome.InvalidState;
            }

            try
            {
                taskRun.Retry(requestedBy, scheduledAtUtc);
            }
            catch (ArgumentException)
            {
                return TaskRunMutationOutcome.InvalidRequest;
            }
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return TaskRunMutationOutcome.Applied;
            }
            catch (DbUpdateConcurrencyException) when (attempt < 2)
            {
                dbContext.ChangeTracker.Clear();
            }
            catch (DbUpdateException)
            {
                return TaskRunMutationOutcome.Conflict;
            }
        }

        return TaskRunMutationOutcome.Conflict;
    }

    public virtual Task<IReadOnlyList<TaskRunSummary>> MarkStaleTimedOutAsync(
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
        return this.MarkStaleLockedAsync(
            nowUtc,
            IsolationLevel.Serializable,
            token => dbContext.Set<TaskRun>()
                .Where(taskRun =>
                    (taskRun.Status == TaskRunStatus.Leased && taskRun.LockedUntilUtc <= nowUtc) ||
                    ((taskRun.Status == TaskRunStatus.Running || taskRun.Status == TaskRunStatus.CancellationRequested) &&
                     ((taskRun.LastHeartbeatAtUtc != null && taskRun.LastHeartbeatAtUtc <= staleBeforeUtc) ||
                      (taskRun.LastHeartbeatAtUtc == null && taskRun.LockedUntilUtc <= nowUtc))))
                .OrderBy(taskRun => taskRun.LockedUntilUtc)
                .ThenBy(taskRun => taskRun.StartedAtUtc)
                .ThenBy(taskRun => taskRun.Id)
                .Take(maxRuns)
                .ToListAsync(token),
            cancellationToken);
    }

    protected async Task<IReadOnlyList<TaskRunSummary>> MarkStaleLockedAsync(
        DateTimeOffset nowUtc,
        IsolationLevel isolationLevel,
        Func<CancellationToken, Task<List<TaskRun>>> loadStaleRuns,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(loadStaleRuns);

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(isolationLevel, cancellationToken)
                .ConfigureAwait(false);

        List<TaskRun> staleRuns = await loadStaleRuns(cancellationToken).ConfigureAwait(false);

        foreach (TaskRun taskRun in staleRuns)
        {
            taskRun.MarkTimedOut(nowUtc);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return staleRuns.Select(ToSummary).ToArray();
    }

    public async Task<TaskControlMessageEnqueueOutcome> EnqueueControlMessageAsync(
        TaskControlMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            bool exists = await dbContext.Set<TaskControlMessageState>()
                .AnyAsync(item => item.Id == message.MessageId, cancellationToken)
                .ConfigureAwait(false);
            if (exists)
            {
                return TaskControlMessageEnqueueOutcome.AlreadyExists;
            }

            TaskRun? taskRun = await dbContext.Set<TaskRun>()
                .FirstOrDefaultAsync(item => item.Id == message.RunId, cancellationToken)
                .ConfigureAwait(false);
            if (taskRun is null)
            {
                return TaskControlMessageEnqueueOutcome.RunNotFound;
            }

            if (!taskRun.RecordControlMessageEnqueued())
            {
                return TaskControlMessageEnqueueOutcome.RunTerminal;
            }

            TaskControlMessageState state = TaskControlMessageState.Enqueue(message);
            dbContext.Set<TaskControlMessageState>().Add(state);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return TaskControlMessageEnqueueOutcome.Enqueued;
            }
            catch (DbUpdateConcurrencyException) when (attempt < 2)
            {
                dbContext.ChangeTracker.Clear();
            }
            catch (DbUpdateException)
            {
                dbContext.ChangeTracker.Clear();
                exists = await dbContext.Set<TaskControlMessageState>()
                    .AnyAsync(item => item.Id == message.MessageId, cancellationToken)
                    .ConfigureAwait(false);
                return exists
                    ? TaskControlMessageEnqueueOutcome.AlreadyExists
                    : TaskControlMessageEnqueueOutcome.Conflict;
            }
        }

        return TaskControlMessageEnqueueOutcome.Conflict;
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

    public Task<TaskRunMutationOutcome> MarkHandledAsync(
        TaskExecutionContext context,
        Guid messageId,
        CancellationToken cancellationToken)
        => this.UpdateOwnedControlMessageAsync(
            context,
            messageId,
            message => TaskControlMessageStatusTransitions.CanMarkHandled(message.Status),
            message => message.MarkHandled(clock.UtcNow),
            cancellationToken);

    public Task<TaskRunMutationOutcome> MarkFailedAsync(
        TaskExecutionContext context,
        Guid messageId,
        string error,
        CancellationToken cancellationToken)
        => this.UpdateOwnedControlMessageAsync(
            context,
            messageId,
            message => TaskControlMessageStatusTransitions.CanMarkFailed(message.Status),
            message => message.MarkFailed(error, clock.UtcNow),
            cancellationToken);

    private async Task<IReadOnlyList<TaskControlMessage>> ReadPendingCoreAsync(
        TaskExecutionContext context,
        int maxMessages,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            TaskRun? taskRun = await this.FindOwnedRunAsync(context, cancellationToken).ConfigureAwait(false);
            if (taskRun is null)
            {
                return [];
            }

            DateTimeOffset nowUtc = clock.UtcNow;
            List<TaskControlMessageState> expired = await dbContext.Set<TaskControlMessageState>()
                .Where(message =>
                    message.RunId == context.RunId &&
                    (message.Status == TaskControlMessageStatus.Pending ||
                     message.Status == TaskControlMessageStatus.Delivered ||
                     message.Status == TaskControlMessageStatus.Failed) &&
                    message.ExpiresAtUtc != null &&
                    message.ExpiresAtUtc <= nowUtc)
                .OrderBy(message => message.ExpiresAtUtc)
                .ThenBy(message => message.Id)
                .Take(maxMessages)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (TaskControlMessageState message in expired)
            {
                message.MarkExpired(nowUtc);
            }

            List<TaskControlMessageState> messages = await dbContext.Set<TaskControlMessageState>()
                .Where(message =>
                    message.RunId == context.RunId &&
                    (message.Status == TaskControlMessageStatus.Pending ||
                     message.Status == TaskControlMessageStatus.Delivered ||
                     message.Status == TaskControlMessageStatus.Failed) &&
                    (message.ExpiresAtUtc == null || message.ExpiresAtUtc > nowUtc))
                .OrderBy(message => message.EnqueuedAtUtc)
                .ThenBy(message => message.Id)
                .Take(maxMessages)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (TaskControlMessageState message in messages)
            {
                message.MarkDelivered(nowUtc);
            }

            if (expired.Count == 0 && messages.Count == 0)
            {
                return [];
            }

            taskRun.FenceLeaseMutation(context);
            try
            {
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
            catch (DbUpdateConcurrencyException)
            {
                dbContext.ChangeTracker.Clear();
            }
        }

        return [];
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

    private static TaskRunDetails ToDetails(TaskRun taskRun) =>
        new(
            ToSummary(taskRun),
            taskRun.Payload,
            taskRun.NodeId,
            taskRun.LeasedAtUtc,
            taskRun.NextAttemptAtUtc,
            taskRun.CancellationRequestedAtUtc,
            taskRun.CancellationRequestedBy);

    private async Task<TaskRun?> FindCanonicalRunAsync(
        TaskRunRequest request,
        CancellationToken cancellationToken)
    {
        TaskRun? byId = await dbContext.Set<TaskRun>()
            .AsNoTracking()
            .FirstOrDefaultAsync(taskRun => taskRun.Id == request.RunId, cancellationToken)
            .ConfigureAwait(false);
        if (byId is not null || request.DeduplicationIdentity is null)
        {
            return byId;
        }

        return await dbContext.Set<TaskRun>()
            .AsNoTracking()
            .Where(taskRun => taskRun.ActiveDeduplicationIdentity == request.DeduplicationIdentity)
            .OrderBy(taskRun => taskRun.CreatedAtUtc)
            .ThenBy(taskRun => taskRun.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<TaskRunMutationOutcome> UpdateOwnedRunAsync(
        TaskExecutionContext context,
        Func<TaskRun, bool> canApply,
        Action<TaskRun> apply,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(canApply);
        ArgumentNullException.ThrowIfNull(apply);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            TaskRun? taskRun = await this.FindOwnedRunAsync(context, cancellationToken).ConfigureAwait(false);
            if (taskRun is null)
            {
                return TaskRunMutationOutcome.LeaseLost;
            }

            if (!canApply(taskRun))
            {
                return TaskRunMutationOutcome.InvalidState;
            }

            apply(taskRun);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return TaskRunMutationOutcome.Applied;
            }
            catch (DbUpdateConcurrencyException) when (attempt < 2)
            {
                dbContext.ChangeTracker.Clear();
            }
        }

        return TaskRunMutationOutcome.Conflict;
    }

    private async Task<TaskRunMutationOutcome> UpdateOwnedControlMessageAsync(
        TaskExecutionContext context,
        Guid messageId,
        Func<TaskControlMessageState, bool> canApply,
        Action<TaskControlMessageState> apply,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(canApply);
        ArgumentNullException.ThrowIfNull(apply);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            TaskRun? taskRun = await this.FindOwnedRunAsync(context, cancellationToken).ConfigureAwait(false);
            if (taskRun is null)
            {
                return TaskRunMutationOutcome.LeaseLost;
            }

            TaskControlMessageState? message = await dbContext.Set<TaskControlMessageState>()
                .FirstOrDefaultAsync(
                    item => item.Id == messageId && item.RunId == context.RunId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (message is null)
            {
                return TaskRunMutationOutcome.NotFound;
            }

            if (!canApply(message))
            {
                return TaskRunMutationOutcome.InvalidState;
            }

            taskRun.FenceLeaseMutation(context);
            apply(message);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return TaskRunMutationOutcome.Applied;
            }
            catch (DbUpdateConcurrencyException) when (attempt < 2)
            {
                dbContext.ChangeTracker.Clear();
            }
        }

        return TaskRunMutationOutcome.Conflict;
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
                    taskRun.NodeId == context.NodeId &&
                    taskRun.LeaseGeneration == context.LeaseGeneration,
                cancellationToken);
    }
}
