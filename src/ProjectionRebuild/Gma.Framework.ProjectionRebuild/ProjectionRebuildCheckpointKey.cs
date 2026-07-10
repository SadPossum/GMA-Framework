namespace Gma.Framework.ProjectionRebuild;

using Gma.Framework.Naming;

public sealed record ProjectionRebuildCheckpointKey
{
    public ProjectionRebuildCheckpointKey(
        string moduleName,
        Guid runId,
        string projectionName,
        string? scopeId)
    {
        this.ModuleName = SharedModuleNames.Normalize(moduleName, nameof(moduleName));
        this.RunId = runId == Guid.Empty
            ? throw new ArgumentException("Projection rebuild run id must not be empty.", nameof(runId))
            : runId;
        this.ProjectionName = ProjectionRebuildNames.NormalizeProjectionName(projectionName, nameof(projectionName));
        this.ScopeId = string.IsNullOrWhiteSpace(scopeId)
            ? null
            : ScopeIds.Normalize(scopeId);
    }

    public string ModuleName { get; }
    public Guid RunId { get; }
    public string ProjectionName { get; }
    public string? ScopeId { get; }
}
