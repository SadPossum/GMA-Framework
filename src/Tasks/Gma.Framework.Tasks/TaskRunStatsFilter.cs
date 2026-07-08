namespace Gma.Framework.Tasks;

public sealed record TaskRunStatsFilter
{
    public TaskRunStatsFilter(
        string? moduleName = null,
        string? taskName = null,
        string? workerGroup = null,
        string? tenantId = null)
    {
        this.ModuleName = string.IsNullOrWhiteSpace(moduleName)
            ? null
            : TaskNames.NormalizeModuleName(moduleName, nameof(moduleName));
        this.TaskName = string.IsNullOrWhiteSpace(taskName)
            ? null
            : TaskNames.NormalizeTaskName(taskName, nameof(taskName));
        this.WorkerGroup = string.IsNullOrWhiteSpace(workerGroup)
            ? null
            : TaskNames.NormalizeWorkerGroup(workerGroup, nameof(workerGroup));
        this.TenantId = string.IsNullOrWhiteSpace(tenantId)
            ? null
            : TaskNames.NormalizeTenantId(tenantId, nameof(tenantId));
    }

    public string? ModuleName { get; }
    public string? TaskName { get; }
    public string? WorkerGroup { get; }
    public string? TenantId { get; }
}
