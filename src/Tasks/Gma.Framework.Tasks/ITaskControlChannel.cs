namespace Gma.Framework.Tasks;

public interface ITaskControlChannel
{
    Task<IReadOnlyList<TaskControlMessage>> ReadPendingAsync(
        TaskExecutionContext context,
        int maxMessages,
        CancellationToken cancellationToken);

    Task<TaskRunMutationOutcome> MarkHandledAsync(
        TaskExecutionContext context,
        Guid messageId,
        CancellationToken cancellationToken);

    Task<TaskRunMutationOutcome> MarkFailedAsync(
        TaskExecutionContext context,
        Guid messageId,
        string error,
        CancellationToken cancellationToken);
}
