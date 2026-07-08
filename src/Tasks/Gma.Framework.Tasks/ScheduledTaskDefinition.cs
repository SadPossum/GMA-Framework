namespace Gma.Framework.Tasks;

public sealed record ScheduledTaskDefinition
{
    public const int ScheduleNameMaxLength = 128;

    public ScheduledTaskDefinition(
        string scheduleName,
        string moduleName,
        string taskName,
        string payloadJson,
        TimeSpan interval,
        string workerGroup = TaskWorkerGroups.Default,
        string? tenantId = null,
        int maxAttempts = 1,
        int payloadVersion = 1,
        string? deduplicationKeyPrefix = null,
        bool runOnStart = false)
    {
        this.ScheduleName = NormalizeScheduleName(scheduleName, nameof(scheduleName));
        this.ModuleName = TaskNames.NormalizeModuleName(moduleName, nameof(moduleName));
        this.TaskName = TaskNames.NormalizeTaskName(taskName, nameof(taskName));
        this.WorkerGroup = TaskNames.NormalizeWorkerGroup(workerGroup, nameof(workerGroup));
        this.PayloadJson = NormalizePayload(payloadJson);
        this.Interval = interval > TimeSpan.Zero
            ? interval
            : throw new ArgumentOutOfRangeException(nameof(interval), interval, "Scheduled task interval must be positive.");
        this.TenantId = string.IsNullOrWhiteSpace(tenantId)
            ? null
            : TaskNames.NormalizeTenantId(tenantId, nameof(tenantId));
        this.MaxAttempts = maxAttempts > 0
            ? maxAttempts
            : throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "Scheduled task max attempts must be positive.");
        this.PayloadVersion = payloadVersion > 0
            ? payloadVersion
            : throw new ArgumentOutOfRangeException(nameof(payloadVersion), payloadVersion, "Scheduled task payload version must be positive.");
        this.DeduplicationKeyPrefix = TaskNames.NormalizeOptionalDeduplicationKey(
                deduplicationKeyPrefix ??
                $"schedule:{this.ModuleName}:{this.TaskName}:{this.ScheduleName}:v{this.PayloadVersion}",
                nameof(deduplicationKeyPrefix)) ??
            throw new ArgumentException("Scheduled task deduplication key prefix is required.", nameof(deduplicationKeyPrefix));
        this.RunOnStart = runOnStart;
    }

    public string ScheduleName { get; }
    public string ModuleName { get; }
    public string TaskName { get; }
    public string WorkerGroup { get; }
    public string PayloadJson { get; }
    public TimeSpan Interval { get; }
    public string? TenantId { get; }
    public int MaxAttempts { get; }
    public int PayloadVersion { get; }
    public string DeduplicationKeyPrefix { get; }
    public bool RunOnStart { get; }

    public string CreateDeduplicationKey(DateTimeOffset occurrenceUtc) =>
        TaskNames.NormalizeOptionalDeduplicationKey(
            $"{this.DeduplicationKeyPrefix}:{occurrenceUtc.UtcDateTime:yyyyMMddHHmmss}",
            nameof(occurrenceUtc))!;

    public static string NormalizeScheduleName(string value, string parameterName = "scheduleName")
    {
        string normalized = TaskNames.NormalizeTaskName(value, parameterName);
        return normalized.Length <= ScheduleNameMaxLength
            ? normalized
            : throw new ArgumentException($"Scheduled task name must be {ScheduleNameMaxLength} characters or fewer.", parameterName);
    }

    private static string NormalizePayload(string payloadJson)
    {
        ArgumentNullException.ThrowIfNull(payloadJson);

        return payloadJson.Length <= TaskRunRequest.PayloadMaxLength
            ? payloadJson
            : throw new ArgumentException(
                $"Scheduled task payload must be {TaskRunRequest.PayloadMaxLength} characters or fewer.",
                nameof(payloadJson));
    }
}
