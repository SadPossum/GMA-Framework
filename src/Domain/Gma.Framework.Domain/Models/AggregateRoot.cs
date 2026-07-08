namespace Gma.Framework.Domain.Models;

using Gma.Framework.Domain;

public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot
    where TId : notnull
{
    private readonly List<IDomainEvent> domainEvents = [];
    private readonly IReadOnlyCollection<IDomainEvent> readOnlyDomainEvents;

    protected AggregateRoot() => this.readOnlyDomainEvents = this.domainEvents.AsReadOnly();

    protected AggregateRoot(TId id)
        : base(id)
        => this.readOnlyDomainEvents = this.domainEvents.AsReadOnly();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => this.readOnlyDomainEvents;

    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        this.domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents() => this.domainEvents.Clear();
}
