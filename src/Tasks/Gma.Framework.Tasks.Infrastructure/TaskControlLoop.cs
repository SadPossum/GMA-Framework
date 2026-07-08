namespace Gma.Framework.Tasks.Infrastructure;

using Gma.Framework.Tasks;

internal sealed class TaskControlLoop(ITaskControlChannel channel) : ITaskControlLoop
{
    public Task<TaskControlPollResult> PollAsync(
        TaskExecutionContext context,
        int maxMessages,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxMessages, 1);

        return this.PollCoreAsync(context, maxMessages, cancellationToken);
    }

    public Task MarkHandledAsync(
        TaskExecutionContext context,
        TaskControlMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(message);

        return channel.MarkHandledAsync(context, message.MessageId, cancellationToken);
    }

    public Task MarkFailedAsync(
        TaskExecutionContext context,
        TaskControlMessage message,
        string error,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(message);

        return channel.MarkFailedAsync(context, message.MessageId, error, cancellationToken);
    }

    private async Task<TaskControlPollResult> PollCoreAsync(
        TaskExecutionContext context,
        int maxMessages,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<TaskControlMessage> messages = await channel
            .ReadPendingAsync(context, maxMessages, cancellationToken)
            .ConfigureAwait(false);

        return new TaskControlPollResult(messages);
    }
}
