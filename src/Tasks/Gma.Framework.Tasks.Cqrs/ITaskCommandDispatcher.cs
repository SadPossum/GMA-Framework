namespace Gma.Framework.Tasks.Cqrs;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Tasks;

public interface ITaskCommandDispatcher
{
    Task<Result<TResponse>> DispatchAsync<TCommand, TResponse>(
        TaskExecutionContext context,
        TCommand command,
        CancellationToken cancellationToken)
        where TCommand : ICommand<TResponse>;
}
