namespace Gma.Framework.Tasks;

public sealed record TaskRunLease
{
    public TaskRunLease(
        Guid runId,
        string moduleName,
        string taskName,
        string workerGroup,
        string workerId,
        string nodeId,
        string payloadJson,
        int attempt,
        DateTimeOffset leasedAtUtc,
        DateTimeOffset lockedUntilUtc,
        string? scopeId = null,
        Guid? correlationId = null,
        bool cancellationRequested = false,
        int payloadVersion = 1)
    {
        this.RunId = TaskRunRequest.RequireId(runId, nameof(runId));
        this.ModuleName = TaskNames.NormalizeModuleName(moduleName, nameof(moduleName));
        this.TaskName = TaskNames.NormalizeTaskName(taskName, nameof(taskName));
        this.WorkerGroup = TaskNames.NormalizeWorkerGroup(workerGroup, nameof(workerGroup));
        this.WorkerId = TaskNames.NormalizeWorkerId(workerId, nameof(workerId));
        this.NodeId = TaskNames.NormalizeWorkerId(nodeId, nameof(nodeId));
        this.PayloadJson = TaskRunRequest.NormalizePayload(payloadJson);
        this.Attempt = attempt > 0
            ? attempt
            : throw new ArgumentOutOfRangeException(nameof(attempt), attempt, "Task lease attempt must be positive.");
        this.PayloadVersion = payloadVersion > 0
            ? payloadVersion
            : throw new ArgumentOutOfRangeException(nameof(payloadVersion), payloadVersion, "Task payload version must be positive.");
        this.LeasedAtUtc = TaskRunRequest.RequireTimestamp(leasedAtUtc, nameof(leasedAtUtc));
        this.LockedUntilUtc = TaskRunRequest.RequireTimestamp(lockedUntilUtc, nameof(lockedUntilUtc));
        this.ScopeId = string.IsNullOrWhiteSpace(scopeId)
            ? null
            : TaskNames.NormalizeScopeId(scopeId, nameof(scopeId));
        this.CorrelationId = correlationId is null
            ? null
            : TaskRunRequest.RequireId(correlationId.Value, nameof(correlationId));
        this.CancellationRequested = cancellationRequested;

        if (this.LockedUntilUtc <= this.LeasedAtUtc)
        {
            throw new ArgumentException("Task lease lock expiration must be after the lease timestamp.", nameof(lockedUntilUtc));
        }
    }

    public Guid RunId { get; }
    public string ModuleName { get; }
    public string TaskName { get; }
    public string WorkerGroup { get; }
    public string WorkerId { get; }
    public string NodeId { get; }
    public string PayloadJson { get; }
    public int Attempt { get; }
    public int PayloadVersion { get; }
    public DateTimeOffset LeasedAtUtc { get; }
    public DateTimeOffset LockedUntilUtc { get; }
    public string? ScopeId { get; }
    public Guid? CorrelationId { get; }
    public bool CancellationRequested { get; }

    public TaskExecutionContext CreateExecutionContext() =>
        new(
            this.RunId,
            this.ModuleName,
            this.TaskName,
            this.WorkerGroup,
            this.WorkerId,
            this.NodeId,
            this.Attempt,
            this.ScopeId,
            this.CorrelationId,
            this.CancellationRequested,
            this.PayloadVersion,
            this.LockedUntilUtc - this.LeasedAtUtc);
}
