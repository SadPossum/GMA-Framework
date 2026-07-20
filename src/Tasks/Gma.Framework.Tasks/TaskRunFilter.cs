namespace Gma.Framework.Tasks;

public sealed record TaskRunFilter
{
    public TaskRunFilter(
        string? moduleName = null,
        string? taskName = null,
        string? workerGroup = null,
        TaskRunStatus? status = null,
        string? scopeId = null,
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
        this.ScopeId = string.IsNullOrWhiteSpace(scopeId)
            ? null
            : TaskNames.NormalizeScopeId(scopeId, nameof(scopeId));
        this.PageSize = Math.Clamp(pageSize, 1, 200);
        int maxPage = (int)Math.Min(
            int.MaxValue,
            ((long)int.MaxValue / this.PageSize) + 1L);
        this.Page = Math.Clamp(page, 1, maxPage);
    }

    public string? ModuleName { get; }
    public string? TaskName { get; }
    public string? WorkerGroup { get; }
    public TaskRunStatus? Status { get; }
    public string? ScopeId { get; }
    public int Page { get; }
    public int PageSize { get; }
    public int SkipCount => (this.Page - 1) * this.PageSize;
}
