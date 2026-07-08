namespace Gma.Framework.Tasks.Cqrs;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Tasks;

internal sealed class TaskCommandDispatcher(IRequestDispatcher dispatcher) : ITaskCommandDispatcher
{
    public Task<Result<TResponse>> DispatchAsync<TCommand, TResponse>(
        TaskExecutionContext context,
        TCommand command,
        CancellationToken cancellationToken)
        where TCommand : ICommand<TResponse>
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);

        return dispatcher.SendAsync(command, cancellationToken);
    }
}
