namespace Gma.Framework.Tasks;

public sealed record TaskRunFilter
{
    public TaskRunFilter(
        string? moduleName = null,
        string? taskName = null,
        string? workerGroup = null,
        TaskRunStatus? status = null,
        string? tenantId = null,
        int page = 1,
        int pageSize = 50)
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
        this.Status = status is null
            ? null
            : TaskRunStatusTransitions.RequireKnown(status.Value);
        this.TenantId = string.IsNullOrWhiteSpace(tenantId)
            ? null
            : TaskNames.NormalizeTenantId(tenantId, nameof(tenantId));
        this.Page = Math.Max(1, page);
        this.PageSize = Math.Clamp(pageSize, 1, 200);
    }

    public string? ModuleName { get; }
    public string? TaskName { get; }
    public string? WorkerGroup { get; }
    public TaskRunStatus? Status { get; }
    public string? TenantId { get; }
    public int Page { get; }
    public int PageSize { get; }
    public int SkipCount => (this.Page - 1) * this.PageSize;
}
