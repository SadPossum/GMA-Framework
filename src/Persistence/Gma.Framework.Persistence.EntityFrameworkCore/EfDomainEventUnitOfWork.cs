namespace Gma.Framework.Persistence.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore;
using Gma.Framework.Application.Events;
using Gma.Framework.Cqrs.UnitOfWork;
using Gma.Framework.Domain;
using Gma.Framework.Naming;
using Gma.Framework.Cqrs;

public abstract class EfDomainEventUnitOfWork<TDbContext>(
    string moduleName,
    TDbContext dbContext,
    IDomainEventDispatcher domainEventDispatcher) : IUnitOfWork
    where TDbContext : DbContext
{
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
}
