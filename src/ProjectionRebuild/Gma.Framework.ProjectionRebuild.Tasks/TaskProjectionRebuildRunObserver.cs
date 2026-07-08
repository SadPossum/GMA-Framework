namespace Gma.Framework.ProjectionRebuild.Tasks;

using Gma.Framework.ProjectionRebuild;
using Gma.Framework.Tasks;

internal sealed class TaskProjectionRebuildRunObserver(
    TaskExecutionContext taskContext,
    ITaskRuntimeReporter reporter,
    ITaskControlLoop controlLoop)
    : IProjectionRebuildRunObserver
{
    public Task PauseIfRequestedAsync(
        ProjectionRebuildExecutionContext context,
        TimeSpan pollInterval,
        int maxMessages,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return controlLoop.PauseIfRequestedAsync(taskContext, pollInterval, maxMessages, cancellationToken);
    }

    public Task ReportProgressAsync(
        ProjectionRebuildExecutionContext context,
        ProjectionRebuildProgress progress,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(progress);

        return reporter.ReportProgressAsync(
            taskContext,
            new TaskProgress(progress.PercentComplete, progress.Message),
            nowUtc,
            cancellationToken);
    }
}
