namespace Gma.Framework.Domain.Models;

using Gma.Framework.Naming;

public abstract class ScopedAggregateRoot<TId> : AggregateRoot<TId>, IScopedEntity
    where TId : notnull
{
    protected ScopedAggregateRoot() { }

    protected ScopedAggregateRoot(TId id, string scopeId)
        : base(id)
        => this.ScopeId = ScopeIds.Normalize(scopeId);

    public string ScopeId { get; private set; } = string.Empty;
}
