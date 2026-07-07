namespace Gma.Framework.Cqrs.Infrastructure;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class ValidationQueryBehavior<TQuery, TResponse>(IEnumerable<IQueryValidator<TQuery>> validators)
    : IQueryPipelineBehavior<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    public Task<Result<TResponse>> HandleAsync(
        TQuery query,
        QueryNext<TResponse> next,
        CancellationToken cancellationToken)
    {
        string[] failures = validators
            .SelectMany(validator => GetFailures(validator, query))
            .Where(failure => !string.IsNullOrWhiteSpace(failure))
            .ToArray();

        return failures.Length == 0
            ? next()
            : Task.FromResult(Result.Failure<TResponse>(RequestValidationErrors.Failed(failures)));
    }

    private static IEnumerable<string> GetFailures(IQueryValidator<TQuery> validator, TQuery query) =>
        validator.Validate(query) ?? throw new InvalidOperationException(
            $"Query validator '{validator.GetType().FullName}' returned a null failure collection for request '{typeof(TQuery).FullName}'.");
}
