namespace Gma.Framework.Cqrs;

using Gma.Framework.Results;

public delegate Task<Result<TResponse>> CommandNext<TResponse>();

public interface ICommandPipelineBehavior<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> HandleAsync(
        TCommand command,
        CommandNext<TResponse> next,
        CancellationToken cancellationToken);
}
