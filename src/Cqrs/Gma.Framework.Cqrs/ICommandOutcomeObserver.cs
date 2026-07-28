namespace Gma.Framework.Cqrs;

using Gma.Framework.Results;

public interface ICommandOutcomeObserver<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task ObserveAsync(
        TCommand command,
        Result<TResponse> result,
        CancellationToken cancellationToken);
}
