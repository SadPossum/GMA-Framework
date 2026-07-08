namespace Gma.Framework.Tasks;

public interface ITaskControlLoop
{
    Task<TaskControlPollResult> PollAsync(
        TaskExecutionContext context,
        int maxMessages,
        CancellationToken cancellationToken);

    Task MarkHandledAsync(
        TaskExecutionContext context,
        TaskControlMessage message,
        CancellationToken cancellationToken);

    Task MarkFailedAsync(
        TaskExecutionContext context,
        TaskControlMessage message,
        string error,
        CancellationToken cancellationToken);
}
