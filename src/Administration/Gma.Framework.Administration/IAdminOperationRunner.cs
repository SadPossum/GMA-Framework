namespace Gma.Framework.Administration;

using Gma.Framework.Results;

public interface IAdminOperationRunner
{
    Task<AdminOperationExecutionResult<T>> ExecuteAsync<T>(
        AdminOperationContext context,
        Func<CancellationToken, Task<Result<T>>> action,
        CancellationToken cancellationToken);
}
