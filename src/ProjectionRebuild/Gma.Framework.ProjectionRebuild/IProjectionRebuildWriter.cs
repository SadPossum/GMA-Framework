namespace Gma.Framework.ProjectionRebuild;

public interface IProjectionRebuildWriter<TSnapshot>
{
    Task<ProjectionWriteResult> WriteAsync(
        ProjectionRebuildRequest request,
        IReadOnlyCollection<TSnapshot> snapshots,
        CancellationToken cancellationToken);
}
