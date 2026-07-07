namespace Gma.Framework.ProjectionRebuild;

public sealed record ProjectionRebuildSummary(
    string ModuleName,
    string ProjectionName,
    string? TenantId,
    bool DryRun,
    ProjectionRebuildCheckpoint Checkpoint);
