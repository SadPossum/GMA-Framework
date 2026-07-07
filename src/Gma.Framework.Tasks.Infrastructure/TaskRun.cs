namespace Gma.Framework.Tasks.Infrastructure;

using Gma.Framework.Tasks;

public class TaskRun
{
    public const int ModuleNameMaxLength = 128;
    public const int TaskNameMaxLength = 128;
    public const int WorkerGroupMaxLength = 128;
    public const int WorkerIdMaxLength = TaskNames.WorkerIdMaxLength;
    public const int ErrorMaxLength = 2048;
    public const int ProgressMessageMaxLength = 1024;
    public const string DefaultError = "Unknown error.";
    public const int DeduplicationKeyMaxLength = TaskNames.DeduplicationKeyMaxLength;

    public Guid Id { get; private set; }
    public string ModuleName { get; private set; } = string.Empty;
    public string TaskName { get; private set; } = string.Empty;
    public string WorkerGroup { get; private set; } = string.Empty;
    public int PayloadVersion { get; private set; }
    public TaskRunStatus Status { get; private set; }
    public string Payload { get; private set; } = string.Empty;
    public string? DeduplicationKey { get; private set; }
    public string? TenantId { get; private set; }
    public Guid? CorrelationId { get; private set; }
    public string? RequestedBy { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ScheduledAtUtc { get; private set; }
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public DateTimeOffset? LeasedAtUtc { get; private set; }
    public DateTimeOffset? LockedUntilUtc { get; private set; }
    public string? LockedBy { get; private set; }
    public string? NodeId { get; private set; }
    public int Attempts { get; private set; }
    public int MaxAttempts { get; private set; }
    public DateTimeOffset? NextAttemptAtUtc { get; private set; }
    public DateTimeOffset? LastHeartbeatAtUtc { get; private set; }
    public int? ProgressPercent { get; private set; }
    public string? ProgressMessage { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset? CancellationRequestedAtUtc { get; private set; }
    public string? CancellationRequestedBy { get; private set; }

    private TaskRun() { }

    private TaskRun(TaskRunRequest request)
    {
        this.Id = request.RunId;
        this.ModuleName = request.ModuleName;
        this.TaskName = request.TaskName;
        this.WorkerGroup = request.WorkerGroup;
        this.PayloadVersion = request.PayloadVersion;
        this.Payload = request.PayloadJson;
        this.DeduplicationKey = request.DeduplicationKey;
        this.TenantId = request.TenantId;
        this.CorrelationId = request.CorrelationId;
        this.RequestedBy = request.RequestedBy;
        this.CreatedAtUtc = request.CreatedAtUtc;
        this.ScheduledAtUtc = request.ScheduledAtUtc;
        this.MaxAttempts = request.MaxAttempts;
        this.Status = TaskRunStatus.Queued;
    }

    public static TaskRun Enqueue(TaskRunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new TaskRun(request);
    }

    public bool CanClaim(DateTimeOffset nowUtc) =>
        TaskRunStatusTransitions.CanClaim(
            this.Status,
            this.ScheduledAtUtc,
            this.LockedUntilUtc,
            this.NextAttemptAtUtc,
            nowUtc);

    public TaskRunLease Claim(TaskWorkerClaim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);

        if (!string.Equals(this.WorkerGroup, claim.WorkerGroup, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Task run belongs to a different worker group.");
        }

        if (!this.CanClaim(claim.ClaimedAtUtc))
        {
            throw new InvalidOperationException("Task run is not claimable.");
        }

        bool cancellationRequested = this.Status == TaskRunStatus.CancellationRequested;
        if (!cancellationRequested && this.Attempts >= this.MaxAttempts)
        {
            this.MarkTerminalFailure("Task run exhausted all retry attempts.", claim.ClaimedAtUtc);
            throw new InvalidOperationException("Task run has exhausted all retry attempts.");
        }

        if (!cancellationRequested)
        {
            this.Attempts++;
        }

        this.Status = cancellationRequested ? TaskRunStatus.CancellationRequested : TaskRunStatus.Leased;
        this.LockedBy = claim.WorkerId;
        this.NodeId = claim.NodeId;
        this.LeasedAtUtc = claim.ClaimedAtUtc;
        this.LockedUntilUtc = claim.LockedUntilUtc;
        this.NextAttemptAtUtc = null;
        this.LastError = null;

        return this.ToLease();
    }

    public void MarkStarted(TaskExecutionContext context, DateTimeOffset startedAtUtc)
    {
        this.EnsureLeaseOwner(context);
        if (!TaskRunStatusTransitions.CanStart(this.Status))
        {
            throw new InvalidOperationException("Task run must be leased before it can start.");
        }

        this.Status = TaskRunStatus.Running;
        this.StartedAtUtc ??= RequireTimestamp(startedAtUtc, nameof(startedAtUtc));
        this.LastHeartbeatAtUtc = this.StartedAtUtc;
        this.RenewLease(context, this.StartedAtUtc.Value);
    }

    public void MarkSucceeded(TaskExecutionContext context, DateTimeOffset completedAtUtc)
    {
        this.EnsureLeaseOwner(context);
        if (!TaskRunStatusTransitions.CanComplete(this.Status))
        {
            throw new InvalidOperationException("Task run must be running before it can complete.");
        }

        this.Status = TaskRunStatus.Succeeded;
        this.CompletedAtUtc = RequireTimestamp(completedAtUtc, nameof(completedAtUtc));
        this.ClearLease();
        this.LastError = null;
        this.NextAttemptAtUtc = null;
    }

    public void MarkCanceled(TaskExecutionContext context, DateTimeOffset canceledAtUtc)
    {
        this.EnsureLeaseOwner(context);
        if (this.Status is not (TaskRunStatus.Leased or TaskRunStatus.Running or TaskRunStatus.CancellationRequested))
        {
            throw new InvalidOperationException("Task run must be leased, running, or cancellation-requested before it can be canceled.");
        }

        this.MarkCanceledCore(canceledAtUtc);
    }

    public void MarkFailed(
        TaskExecutionContext context,
        string error,
        DateTimeOffset failedAtUtc,
        DateTimeOffset? retryAtUtc)
    {
        this.EnsureLeaseOwner(context);
        if (!TaskRunStatusTransitions.CanComplete(this.Status) && this.Status != TaskRunStatus.Leased)
        {
            throw new InvalidOperationException("Task run must be leased or running before it can fail.");
        }

        DateTimeOffset normalizedFailedAtUtc = RequireTimestamp(failedAtUtc, nameof(failedAtUtc));
        this.LastError = NormalizeError(error);
        this.CompletedAtUtc = retryAtUtc is null || this.Attempts >= this.MaxAttempts
            ? normalizedFailedAtUtc
            : null;
        this.Status = retryAtUtc is not null && this.Attempts < this.MaxAttempts
            ? TaskRunStatus.RetryScheduled
            : TaskRunStatus.Failed;
        this.NextAttemptAtUtc = this.Status == TaskRunStatus.RetryScheduled
            ? ValidateRetryAt(retryAtUtc!.Value, normalizedFailedAtUtc)
            : null;
        this.ClearLease();
    }

    public void MarkTimedOut(DateTimeOffset timedOutAtUtc)
    {
        if (TaskRunStatusTransitions.IsTerminal(this.Status))
        {
            return;
        }

        this.LastError = NormalizeError("Task run timed out after missing heartbeat or exceeding its lease window.");
        this.Status = TaskRunStatus.TimedOut;
        this.CompletedAtUtc = RequireTimestamp(timedOutAtUtc, nameof(timedOutAtUtc));
        this.NextAttemptAtUtc = null;
        this.ClearLease();
    }

    public void MarkHeartbeat(TaskExecutionContext context, DateTimeOffset nowUtc)
    {
        this.EnsureLeaseOwner(context);
        if (this.Status is not (TaskRunStatus.Running or TaskRunStatus.CancellationRequested))
        {
            throw new InvalidOperationException("Task run must be running before heartbeat can be recorded.");
        }

        this.LastHeartbeatAtUtc = RequireTimestamp(nowUtc, nameof(nowUtc));
        this.RenewLease(context, this.LastHeartbeatAtUtc.Value);
    }

    public void MarkProgress(TaskExecutionContext context, TaskProgress progress, DateTimeOffset nowUtc)
    {
        this.MarkHeartbeat(context, nowUtc);
        this.ProgressPercent = progress.PercentComplete;
        this.ProgressMessage = NormalizeOptional(progress.Message, ProgressMessageMaxLength);
    }

    public void RequestCancellation(string? requestedBy, DateTimeOffset requestedAtUtc)
    {
        if (!TaskRunStatusTransitions.CanRequestCancellation(this.Status))
        {
            return;
        }

        this.CancellationRequestedBy = TaskNames.NormalizeOptionalActor(requestedBy, nameof(requestedBy));
        this.CancellationRequestedAtUtc = RequireTimestamp(requestedAtUtc, nameof(requestedAtUtc));

        if (this.Status is TaskRunStatus.Leased or TaskRunStatus.Running)
        {
            this.Status = TaskRunStatus.CancellationRequested;
            return;
        }

        this.MarkCanceledCore(requestedAtUtc);
    }

    public void Retry(string? requestedBy, DateTimeOffset scheduledAtUtc)
    {
        if (this.Status is not (TaskRunStatus.Failed or TaskRunStatus.TimedOut or TaskRunStatus.Canceled or TaskRunStatus.RetryScheduled))
        {
            throw new InvalidOperationException("Task run must be failed, timed out, canceled, or retry-scheduled before it can be retried.");
        }

        DateTimeOffset normalizedScheduledAtUtc = RequireTimestamp(scheduledAtUtc, nameof(scheduledAtUtc));
        if (normalizedScheduledAtUtc < this.CreatedAtUtc)
        {
            throw new ArgumentException("Task retry schedule timestamp cannot be earlier than the created timestamp.", nameof(scheduledAtUtc));
        }

        this.Status = TaskRunStatus.Queued;
        this.ScheduledAtUtc = normalizedScheduledAtUtc;
        this.StartedAtUtc = null;
        this.CompletedAtUtc = null;
        this.LeasedAtUtc = null;
        this.LockedUntilUtc = null;
        this.LockedBy = null;
        this.NodeId = null;
        this.Attempts = 0;
        this.NextAttemptAtUtc = null;
        this.LastHeartbeatAtUtc = null;
        this.LastError = null;
        this.CancellationRequestedAtUtc = null;
        this.CancellationRequestedBy = null;
        this.RequestedBy = TaskNames.NormalizeOptionalActor(requestedBy, nameof(requestedBy)) ?? this.RequestedBy;
    }

    public TaskRunLease ToLease()
    {
        if (string.IsNullOrWhiteSpace(this.LockedBy) ||
            string.IsNullOrWhiteSpace(this.NodeId) ||
            this.LockedUntilUtc is null)
        {
            throw new InvalidOperationException("Task run is not currently leased.");
        }

        return new TaskRunLease(
            this.Id,
            this.ModuleName,
            this.TaskName,
            this.WorkerGroup,
            this.LockedBy,
            this.NodeId,
            this.Payload,
            this.Attempts,
            this.LeasedAtUtc ?? throw new InvalidOperationException("Task run has no lease timestamp."),
            this.LockedUntilUtc.Value,
            this.TenantId,
            this.CorrelationId,
            this.Status == TaskRunStatus.CancellationRequested,
            this.PayloadVersion);
    }

    private void EnsureLeaseOwner(TaskExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (this.Id != context.RunId ||
            !string.Equals(this.ModuleName, context.ModuleName, StringComparison.Ordinal) ||
            !string.Equals(this.TaskName, context.TaskName, StringComparison.Ordinal) ||
            !string.Equals(this.WorkerGroup, context.WorkerGroup, StringComparison.Ordinal) ||
            this.PayloadVersion != context.PayloadVersion ||
            !string.Equals(this.LockedBy, context.WorkerId, StringComparison.Ordinal) ||
            !string.Equals(this.NodeId, context.NodeId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Task run lease is owned by a different worker.");
        }
    }

    private void ClearLease()
    {
        this.LockedBy = null;
        this.LockedUntilUtc = null;
        this.NodeId = null;
        this.LeasedAtUtc = null;
    }

    private void RenewLease(TaskExecutionContext context, DateTimeOffset observedAtUtc)
    {
        if (context.LeaseExtension is null)
        {
            return;
        }

        DateTimeOffset renewedUntilUtc = observedAtUtc.Add(context.LeaseExtension.Value);
        if (this.LockedUntilUtc is null || renewedUntilUtc > this.LockedUntilUtc)
        {
            this.LockedUntilUtc = renewedUntilUtc;
        }
    }

    private void MarkTerminalFailure(string error, DateTimeOffset failedAtUtc)
    {
        this.LastError = NormalizeError(error);
        this.Status = TaskRunStatus.Failed;
        this.CompletedAtUtc = RequireTimestamp(failedAtUtc, nameof(failedAtUtc));
        this.NextAttemptAtUtc = null;
        this.ClearLease();
    }

    private void MarkCanceledCore(DateTimeOffset canceledAtUtc)
    {
        this.Status = TaskRunStatus.Canceled;
        this.CompletedAtUtc = RequireTimestamp(canceledAtUtc, nameof(canceledAtUtc));
        this.NextAttemptAtUtc = null;
        this.LastError = null;
        this.ClearLease();
    }

    private static DateTimeOffset ValidateRetryAt(DateTimeOffset retryAtUtc, DateTimeOffset failedAtUtc) =>
        retryAtUtc <= failedAtUtc
            ? throw new ArgumentException("Task retry timestamp must be after the failure timestamp.", nameof(retryAtUtc))
            : retryAtUtc;

    public static string NormalizeError(string error)
    {
        string normalized = string.IsNullOrWhiteSpace(error)
            ? DefaultError
            : error.Trim();

        return normalized.Length > ErrorMaxLength
            ? normalized[..ErrorMaxLength]
            : normalized;
    }

    public static DateTimeOffset RequireTimestamp(DateTimeOffset value, string parameterName) =>
        value == default
            ? throw new ArgumentException($"{parameterName} must not be the default timestamp.", parameterName)
            : value;

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim();
        return normalized.Length > maxLength
            ? normalized[..maxLength]
            : normalized;
    }
}
