namespace Gma.Framework.ProjectionRebuild;

public interface IProjectionRebuildSource<TSnapshot>
{
    Task<ProjectionReadBatch<TSnapshot>> ReadAsync(
        ProjectionRebuildRequest request,
        string? cursor,
        CancellationToken cancellationToken);
}
