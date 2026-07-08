namespace Gma.Framework.ProjectionRebuild;

public interface IProjectionRebuildRunObserver
{
    Task PauseIfRequestedAsync(
        ProjectionRebuildExecutionContext context,
        TimeSpan pollInterval,
        int maxMessages,
        CancellationToken cancellationToken);

    Task ReportProgressAsync(
        ProjectionRebuildExecutionContext context,
        ProjectionRebuildProgress progress,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
}
