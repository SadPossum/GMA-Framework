namespace Gma.Framework.Persistence.EntityFrameworkCore;

using Gma.Framework.Application.Events;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Microsoft.EntityFrameworkCore;

public abstract class EfDomainEventInboxStore<TDbContext>(
    TDbContext dbContext,
    ISystemClock clock,
    IIdGenerator idGenerator,
    IDomainEventDispatcher domainEventDispatcher,
    string moduleName)
    : EfInboxStore<TDbContext>(dbContext, clock, idGenerator, moduleName)
    where TDbContext : DbContext
{
    protected override Task SaveChangesAsync(CancellationToken cancellationToken) =>
        EfDomainEventSaveChanges.SaveAsync(
            this.ModuleName,
            this.DbContext,
            domainEventDispatcher,
            cancellationToken);
}
