namespace Gma.Framework.ProjectionRebuild;

public static class ProjectionRebuildRunObserver
{
    public static IProjectionRebuildRunObserver None { get; } = new NoopProjectionRebuildRunObserver();

    private sealed class NoopProjectionRebuildRunObserver : IProjectionRebuildRunObserver
    {
        public Task PauseIfRequestedAsync(
            ProjectionRebuildExecutionContext context,
            TimeSpan pollInterval,
            int maxMessages,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            return Task.CompletedTask;
        }

        public Task ReportProgressAsync(
            ProjectionRebuildExecutionContext context,
            ProjectionRebuildProgress progress,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(progress);
            return Task.CompletedTask;
        }
    }
}
