namespace Gma.Framework.Tasks;

public interface ITaskRunStore : ITaskRuntimeReporter, ITaskControlChannel
{
    Task<TaskRunEnqueueResult> EnqueueAsync(TaskRunRequest request, CancellationToken cancellationToken);

    Task<TaskRunPage> ListAsync(
        TaskRunFilter filter,
        CancellationToken cancellationToken);

    Task<TaskRunDetails?> GetAsync(
        Guid runId,
        CancellationToken cancellationToken);

    Task<TaskRunStats> GetStatsAsync(
        TaskRunStatsFilter filter,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskRunLease>> ClaimReadyAsync(
        TaskWorkerClaim claim,
        CancellationToken cancellationToken);

    Task<TaskRunMutationOutcome> MarkStartedAsync(
        TaskExecutionContext context,
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken);

    Task<TaskRunMutationOutcome> MarkSucceededAsync(
        TaskExecutionContext context,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken);

    Task<TaskRunMutationOutcome> MarkCanceledAsync(
        TaskExecutionContext context,
        DateTimeOffset canceledAtUtc,
        CancellationToken cancellationToken);

    Task<TaskRunMutationOutcome> MarkFailedAsync(
        TaskExecutionContext context,
        string error,
        DateTimeOffset failedAtUtc,
        DateTimeOffset? retryAtUtc,
        CancellationToken cancellationToken);

    Task<TaskRunMutationOutcome> RequestCancellationAsync(
        Guid runId,
        string? requestedBy,
        DateTimeOffset requestedAtUtc,
        CancellationToken cancellationToken);

    Task<TaskRunMutationOutcome> RetryAsync(
        Guid runId,
        string? requestedBy,
        DateTimeOffset scheduledAtUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskRunSummary>> MarkStaleTimedOutAsync(
        DateTimeOffset nowUtc,
        TimeSpan staleAfter,
        int maxRuns,
        CancellationToken cancellationToken);

    Task<TaskControlMessageEnqueueOutcome> EnqueueControlMessageAsync(
        TaskControlMessage message,
        CancellationToken cancellationToken);
}
