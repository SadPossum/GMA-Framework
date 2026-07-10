namespace Gma.Framework.Tasks;

public sealed record TaskRunStatsFilter
{
    public TaskRunStatsFilter(
        string? moduleName = null,
        string? taskName = null,
        string? workerGroup = null,
        string? scopeId = null)
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
        this.ScopeId = string.IsNullOrWhiteSpace(scopeId)
            ? null
            : TaskNames.NormalizeScopeId(scopeId, nameof(scopeId));
    }

    public string? ModuleName { get; }
    public string? TaskName { get; }
    public string? WorkerGroup { get; }
    public string? ScopeId { get; }
}
