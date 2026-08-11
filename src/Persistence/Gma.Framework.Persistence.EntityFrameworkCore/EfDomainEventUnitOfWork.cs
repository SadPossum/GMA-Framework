namespace Gma.Framework.Persistence.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore;
using Gma.Framework.Application.Events;
using Gma.Framework.Cqrs.UnitOfWork;
using Gma.Framework.Naming;
using Gma.Framework.Cqrs;
using Microsoft.EntityFrameworkCore.Storage;

public abstract class EfDomainEventUnitOfWork<TDbContext>(
    string moduleName,
    TDbContext dbContext,
    IDomainEventDispatcher domainEventDispatcher) : ITransactionalUnitOfWork
    where TDbContext : DbContext
{
    private IDbContextTransaction? ownedTransaction;

    public string ModuleName { get; } = SharedNameSegments.NormalizeKebabSegment(moduleName, "module name", nameof(moduleName));

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        EfDomainEventSaveChanges.SaveAsync(
            this.ModuleName,
            dbContext,
            domainEventDispatcher,
            cancellationToken);

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (this.ownedTransaction is not null)
        {
            throw new InvalidOperationException("The unit of work already owns an active transaction.");
        }

        if (dbContext.Database.CurrentTransaction is not null ||
            string.Equals(
                dbContext.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.InMemory",
                StringComparison.Ordinal))
        {
            return;
        }

        this.ownedTransaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (this.ownedTransaction is null)
        {
            return;
        }

        IDbContextTransaction transaction = this.ownedTransaction;
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        this.ownedTransaction = null;
        await transaction.DisposeAsync().ConfigureAwait(false);
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (this.ownedTransaction is null)
        {
            return;
        }

        IDbContextTransaction transaction = this.ownedTransaction;
        try
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.ownedTransaction = null;
            await transaction.DisposeAsync().ConfigureAwait(false);
        }
    }
}
