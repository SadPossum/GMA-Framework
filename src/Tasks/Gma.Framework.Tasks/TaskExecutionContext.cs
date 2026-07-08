namespace Gma.Framework.Tasks;

public sealed record TaskExecutionContext
{
    public TaskExecutionContext(
        Guid runId,
        string moduleName,
        string taskName,
        string workerGroup,
        string workerId,
        string nodeId,
        int attempt,
        string? tenantId = null,
        Guid? correlationId = null,
        bool cancellationRequested = false,
        int payloadVersion = 1,
        TimeSpan? leaseExtension = null)
    {
        this.RunId = RequireId(runId, nameof(runId));
        this.ModuleName = TaskNames.NormalizeModuleName(moduleName, nameof(moduleName));
        this.TaskName = TaskNames.NormalizeTaskName(taskName, nameof(taskName));
        this.WorkerGroup = TaskNames.NormalizeWorkerGroup(workerGroup, nameof(workerGroup));
        this.WorkerId = TaskNames.NormalizeWorkerId(workerId, nameof(workerId));
        this.NodeId = TaskNames.NormalizeWorkerId(nodeId, nameof(nodeId));
        this.Attempt = attempt > 0
            ? attempt
            : throw new ArgumentOutOfRangeException(nameof(attempt), attempt, "Task attempt must be positive.");
        this.PayloadVersion = payloadVersion > 0
            ? payloadVersion
            : throw new ArgumentOutOfRangeException(nameof(payloadVersion), payloadVersion, "Task payload version must be positive.");
        this.TenantId = string.IsNullOrWhiteSpace(tenantId)
            ? null
            : TaskNames.NormalizeTenantId(tenantId, nameof(tenantId));
        this.CorrelationId = correlationId is null
            ? null
            : RequireId(correlationId.Value, nameof(correlationId));
        this.CancellationRequested = cancellationRequested;
        this.LeaseExtension = leaseExtension switch
        {
            null => null,
            { } value when value > TimeSpan.Zero => value,
            { } value => throw new ArgumentOutOfRangeException(
                nameof(leaseExtension),
                value,
                "Task lease extension must be positive when provided.")
        };
    }

    public Guid RunId { get; }
    public string ModuleName { get; }
    public string TaskName { get; }
    public string WorkerGroup { get; }
    public string WorkerId { get; }
    public string NodeId { get; }
    public int Attempt { get; }
    public int PayloadVersion { get; }
    public string? TenantId { get; }
    public Guid? CorrelationId { get; }
    public bool CancellationRequested { get; }
    public TimeSpan? LeaseExtension { get; }

    private static Guid RequireId(Guid id, string parameterName) =>
        id == Guid.Empty
            ? throw new ArgumentException($"{parameterName} must not be empty.", parameterName)
            : id;
}
