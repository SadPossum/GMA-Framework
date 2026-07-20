namespace Gma.Framework.Persistence.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore;
using Gma.Framework.Application.Events;
using Gma.Framework.Cqrs.UnitOfWork;
using Gma.Framework.Domain;
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

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        List<IAggregateRoot> aggregatesWithEvents = dbContext.ChangeTracker
            .Entries()
            .Select(entry => entry.Entity)
            .OfType<IAggregateRoot>()
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .Distinct()
            .ToList();

        List<IDomainEvent> domainEvents = aggregatesWithEvents
            .SelectMany(aggregate => aggregate.DomainEvents)
            .ToList();

        if (domainEvents.Count > 0)
        {
            await domainEventDispatcher.DispatchAsync(domainEvents, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new OptimisticConcurrencyException(this.ModuleName, exception);
        }

        foreach (IAggregateRoot aggregate in aggregatesWithEvents)
        {
            aggregate.ClearDomainEvents();
        }
    }

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
