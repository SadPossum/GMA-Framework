namespace Gma.Framework.Tasks;

public interface ITaskRuntimeReporter
{
    Task<TaskRunMutationOutcome> ReportHeartbeatAsync(
        TaskExecutionContext context,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken);

    Task<TaskRunMutationOutcome> ReportProgressAsync(
        TaskExecutionContext context,
        TaskProgress progress,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken);
}
