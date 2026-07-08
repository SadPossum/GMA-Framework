namespace Gma.Framework.Tasks;

public interface ITaskRuntimeReporter
{
    Task ReportHeartbeatAsync(
        TaskExecutionContext context,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken);

    Task ReportProgressAsync(
        TaskExecutionContext context,
        TaskProgress progress,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken);
}
