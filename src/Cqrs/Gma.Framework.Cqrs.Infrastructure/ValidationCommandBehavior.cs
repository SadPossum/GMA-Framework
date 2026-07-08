namespace Gma.Framework.Cqrs.Infrastructure;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class ValidationCommandBehavior<TCommand, TResponse>(IEnumerable<ICommandValidator<TCommand>> validators)
    : ICommandPipelineBehavior<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public Task<Result<TResponse>> HandleAsync(
        TCommand command,
        CommandNext<TResponse> next,
        CancellationToken cancellationToken)
    {
        string[] failures = validators
            .SelectMany(validator => GetFailures(validator, command))
            .Where(failure => !string.IsNullOrWhiteSpace(failure))
            .ToArray();

        return failures.Length == 0
            ? next()
            : Task.FromResult(Result.Failure<TResponse>(RequestValidationErrors.Failed(failures)));
    }

    private static IEnumerable<string> GetFailures(ICommandValidator<TCommand> validator, TCommand command) =>
        validator.Validate(command) ?? throw new InvalidOperationException(
            $"Command validator '{validator.GetType().FullName}' returned a null failure collection for request '{typeof(TCommand).FullName}'.");
}
