namespace Gma.Framework.ProjectionRebuild;

public interface IProjectionRebuildTransactionBoundary
{
    string ModuleName { get; }

    Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken);
}
