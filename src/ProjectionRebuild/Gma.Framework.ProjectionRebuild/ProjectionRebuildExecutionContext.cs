namespace Gma.Framework.ProjectionRebuild;

using Gma.Framework.Naming;

public sealed record ProjectionRebuildExecutionContext
{
    public ProjectionRebuildExecutionContext(Guid runId, string? tenantId)
    {
        this.RunId = runId == Guid.Empty
            ? throw new ArgumentException("Projection rebuild run id must not be empty.", nameof(runId))
            : runId;
        this.TenantId = string.IsNullOrWhiteSpace(tenantId)
            ? null
            : TenantIds.Normalize(tenantId);
    }

    public Guid RunId { get; }
    public string? TenantId { get; }
}
