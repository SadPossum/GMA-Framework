namespace Gma.Framework.ProjectionRebuild;

public interface IProjectionRebuildCheckpointStore
{
    string ModuleName { get; }

    Task<ProjectionRebuildCheckpoint?> GetAsync(
        ProjectionRebuildCheckpointKey key,
        CancellationToken cancellationToken);

    Task SaveAsync(
        ProjectionRebuildCheckpointKey key,
        ProjectionRebuildCheckpoint checkpoint,
        CancellationToken cancellationToken);
}
