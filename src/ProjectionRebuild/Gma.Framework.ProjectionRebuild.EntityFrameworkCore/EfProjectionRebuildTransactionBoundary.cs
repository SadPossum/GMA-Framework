namespace Gma.Framework.ProjectionRebuild.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore;
using Gma.Framework.Naming;

public abstract class EfProjectionRebuildTransactionBoundary<TDbContext>(
    TDbContext dbContext,
    string moduleName)
    : IProjectionRebuildTransactionBoundary
    where TDbContext : DbContext
{
    public string ModuleName { get; } = SharedModuleNames.Normalize(moduleName);

    public async Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (dbContext.Database.CurrentTransaction is not null)
        {
            return await operation(cancellationToken).ConfigureAwait(false);
        }

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        TResult result = await operation(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }
}
