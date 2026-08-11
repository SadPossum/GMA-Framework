namespace Gma.Framework.Persistence.EntityFrameworkCore;

using Gma.Framework.Application.Events;
using Gma.Framework.Cqrs;
using Gma.Framework.Domain;
using Microsoft.EntityFrameworkCore;

internal static class EfDomainEventSaveChanges
{
    public static async Task SaveAsync(
        string moduleName,
        DbContext dbContext,
        IDomainEventDispatcher domainEventDispatcher,
        CancellationToken cancellationToken)
    {
        List<IAggregateRoot> aggregatesWithEvents = dbContext.ChangeTracker
            .Entries()
            .Select(entry => entry.Entity)
            .OfType<IAggregateRoot>()
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .Distinct()
            .ToList();
        IDomainEvent[] domainEvents = aggregatesWithEvents
            .SelectMany(aggregate => aggregate.DomainEvents)
            .ToArray();

        if (domainEvents.Length > 0)
        {
            await domainEventDispatcher
                .DispatchAsync(domainEvents, cancellationToken)
                .ConfigureAwait(false);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new OptimisticConcurrencyException(moduleName, exception);
        }

        foreach (IAggregateRoot aggregate in aggregatesWithEvents)
        {
            aggregate.ClearDomainEvents();
        }
    }
}
