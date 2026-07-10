namespace Gma.Framework.ProjectionRebuild;

public sealed record ProjectionRebuildSummary(
    string ModuleName,
    string ProjectionName,
    string? ScopeId,
    bool DryRun,
    ProjectionRebuildCheckpoint Checkpoint);
