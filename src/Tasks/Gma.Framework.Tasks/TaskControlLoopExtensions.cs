namespace Gma.Framework.Tasks;

public static class TaskControlLoopExtensions
{
    public static async Task ThrowIfCancellationRequestedAsync(
        this ITaskControlLoop controlLoop,
        TaskExecutionContext context,
        int maxMessages,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(controlLoop);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxMessages, 1);

        TaskControlPollResult control = await controlLoop
            .PollAsync(context, maxMessages, cancellationToken)
            .ConfigureAwait(false);

        await ThrowIfCancellationRequestedAsync(controlLoop, context, control, cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task PauseIfRequestedAsync(
        this ITaskControlLoop controlLoop,
        TaskExecutionContext context,
        TimeSpan pollInterval,
        int maxMessages,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(controlLoop);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxMessages, 1);
        if (pollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pollInterval), pollInterval, "Control polling interval must be positive.");
        }

        TaskControlPollResult control = await controlLoop
            .PollAsync(context, maxMessages, cancellationToken)
            .ConfigureAwait(false);

        await ThrowIfCancellationRequestedAsync(controlLoop, context, control, cancellationToken)
            .ConfigureAwait(false);

        if (!control.PauseRequested)
        {
            return;
        }

        foreach (TaskControlMessage message in control.Messages.Where(message =>
                     string.Equals(message.CommandName, TaskControlCommandNames.Pause, StringComparison.Ordinal)))
        {
            await controlLoop.MarkHandledAsync(context, message, cancellationToken).ConfigureAwait(false);
        }

        while (true)
        {
            await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
            control = await controlLoop.PollAsync(context, maxMessages, cancellationToken).ConfigureAwait(false);

            await ThrowIfCancellationRequestedAsync(controlLoop, context, control, cancellationToken)
                .ConfigureAwait(false);

            TaskControlMessage? resume = control.Messages.FirstOrDefault(message =>
                string.Equals(message.CommandName, TaskControlCommandNames.Resume, StringComparison.Ordinal));
            if (resume is not null)
            {
                await controlLoop.MarkHandledAsync(context, resume, cancellationToken).ConfigureAwait(false);
                return;
            }
        }
    }

    private static async Task ThrowIfCancellationRequestedAsync(
        ITaskControlLoop controlLoop,
        TaskExecutionContext context,
        TaskControlPollResult control,
        CancellationToken cancellationToken)
    {
        if (control.CancellationMessage is not TaskControlMessage cancellationMessage)
        {
            return;
        }

        await controlLoop.MarkHandledAsync(context, cancellationMessage, cancellationToken).ConfigureAwait(false);
        throw new TaskRunCanceledException(
            $"Task run canceled by control command '{cancellationMessage.CommandName}'.");
    }
}
