namespace Gma.Framework.Tasks;

public interface ITaskRunStore : ITaskRuntimeReporter, ITaskControlChannel
{
    Task EnqueueAsync(TaskRunRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskRunSummary>> ListAsync(
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

    Task MarkStartedAsync(
        TaskExecutionContext context,
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken);

    Task MarkSucceededAsync(
        TaskExecutionContext context,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken);

    Task MarkCanceledAsync(
        TaskExecutionContext context,
        DateTimeOffset canceledAtUtc,
        CancellationToken cancellationToken);

    Task MarkFailedAsync(
        TaskExecutionContext context,
        string error,
        DateTimeOffset failedAtUtc,
        DateTimeOffset? retryAtUtc,
        CancellationToken cancellationToken);

    Task RequestCancellationAsync(
        Guid runId,
        string? requestedBy,
        DateTimeOffset requestedAtUtc,
        CancellationToken cancellationToken);

    Task RetryAsync(
        Guid runId,
        string? requestedBy,
        DateTimeOffset scheduledAtUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskRunSummary>> MarkStaleTimedOutAsync(
        DateTimeOffset nowUtc,
        TimeSpan staleAfter,
        int maxRuns,
        CancellationToken cancellationToken);

    Task EnqueueControlMessageAsync(
        TaskControlMessage message,
        CancellationToken cancellationToken);
}
