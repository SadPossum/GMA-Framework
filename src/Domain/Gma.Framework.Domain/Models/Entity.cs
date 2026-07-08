namespace Gma.Framework.Domain.Models;

public abstract class Entity<TId>
    where TId : notnull
{
    protected Entity() => this.Id = default!;

    protected Entity(TId id) => this.Id = id;

    public TId Id { get; protected set; }
}
