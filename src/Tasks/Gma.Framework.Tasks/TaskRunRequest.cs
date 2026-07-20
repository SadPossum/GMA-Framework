namespace Gma.Framework.Tasks;

public sealed record TaskRunRequest
{
    public const int PayloadMaxLength = 256 * 1024;
    public const int DeduplicationIdentityMaxLength = 768;
    private const char DeduplicationIdentitySeparator = '\u001F';

    public TaskRunRequest(
        Guid runId,
        string moduleName,
        string taskName,
        string payloadJson,
        DateTimeOffset createdAtUtc,
        DateTimeOffset scheduledAtUtc,
        string workerGroup = TaskWorkerGroups.Default,
        string? scopeId = null,
        Guid? correlationId = null,
        string? requestedBy = null,
        int maxAttempts = 1,
        int payloadVersion = 1,
        string? deduplicationKey = null)
    {
        this.RunId = RequireId(runId, nameof(runId));
        this.ModuleName = TaskNames.NormalizeModuleName(moduleName, nameof(moduleName));
        this.TaskName = TaskNames.NormalizeTaskName(taskName, nameof(taskName));
        this.WorkerGroup = TaskNames.NormalizeWorkerGroup(workerGroup, nameof(workerGroup));
        this.PayloadJson = NormalizePayload(payloadJson);
        this.CreatedAtUtc = RequireTimestamp(createdAtUtc, nameof(createdAtUtc));
        this.ScheduledAtUtc = RequireTimestamp(scheduledAtUtc, nameof(scheduledAtUtc));
        this.ScopeId = string.IsNullOrWhiteSpace(scopeId)
            ? null
            : TaskNames.NormalizeScopeId(scopeId, nameof(scopeId));
        this.CorrelationId = correlationId is null
            ? null
            : RequireId(correlationId.Value, nameof(correlationId));
        this.RequestedBy = TaskNames.NormalizeOptionalActor(requestedBy, nameof(requestedBy));
        this.MaxAttempts = maxAttempts > 0
            ? maxAttempts
            : throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "Task max attempts must be positive.");
        this.PayloadVersion = payloadVersion > 0
            ? payloadVersion
            : throw new ArgumentOutOfRangeException(nameof(payloadVersion), payloadVersion, "Task payload version must be positive.");
        this.DeduplicationKey = TaskNames.NormalizeOptionalDeduplicationKey(deduplicationKey, nameof(deduplicationKey));
        this.DeduplicationIdentity = this.DeduplicationKey is null
            ? null
            : CreateDeduplicationIdentity(
                this.ModuleName,
                this.TaskName,
                this.ScopeId,
                this.DeduplicationKey);

        if (this.ScheduledAtUtc < this.CreatedAtUtc)
        {
            throw new ArgumentException("Task scheduled timestamp cannot be earlier than the created timestamp.", nameof(scheduledAtUtc));
        }
    }

    public Guid RunId { get; }
    public string ModuleName { get; }
    public string TaskName { get; }
    public string WorkerGroup { get; }
    public string PayloadJson { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset ScheduledAtUtc { get; }
    public string? ScopeId { get; }
    public Guid? CorrelationId { get; }
    public string? RequestedBy { get; }
    public int MaxAttempts { get; }
    public int PayloadVersion { get; }
    public string? DeduplicationKey { get; }
    public string? DeduplicationIdentity { get; }

    internal static Guid RequireId(Guid id, string parameterName) =>
        id == Guid.Empty
            ? throw new ArgumentException($"{parameterName} must not be empty.", parameterName)
            : id;

    internal static DateTimeOffset RequireTimestamp(DateTimeOffset timestamp, string parameterName) =>
        timestamp == default
            ? throw new ArgumentException($"{parameterName} must not be the default timestamp.", parameterName)
            : timestamp;

    internal static string NormalizePayload(string payloadJson)
    {
        ArgumentNullException.ThrowIfNull(payloadJson);

        if (payloadJson.Length > PayloadMaxLength)
        {
            throw new ArgumentException(
                $"Task payload must be {PayloadMaxLength} characters or fewer.",
                nameof(payloadJson));
        }

        return payloadJson;
    }

    public static string? CreateDeduplicationIdentity(
        string moduleName,
        string taskName,
        string? scopeId,
        string? deduplicationKey)
    {
        if (deduplicationKey is null)
        {
            return null;
        }

        return string.Concat(
            moduleName,
            DeduplicationIdentitySeparator,
            taskName,
            DeduplicationIdentitySeparator,
            scopeId,
            DeduplicationIdentitySeparator,
            deduplicationKey);
    }
}
