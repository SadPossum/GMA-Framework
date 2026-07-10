namespace Gma.Framework.ProjectionRebuild;

using Gma.Framework.Naming;

public sealed record ProjectionRebuildExecutionContext
{
    public ProjectionRebuildExecutionContext(Guid runId, string? scopeId)
    {
        this.RunId = runId == Guid.Empty
            ? throw new ArgumentException("Projection rebuild run id must not be empty.", nameof(runId))
            : runId;
        this.ScopeId = string.IsNullOrWhiteSpace(scopeId)
            ? null
            : ScopeIds.Normalize(scopeId);
    }

    public Guid RunId { get; }
    public string? ScopeId { get; }
}
