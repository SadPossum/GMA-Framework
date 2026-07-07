namespace Gma.Framework.Application.Events;

using Gma.Framework.Domain;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken);
}
