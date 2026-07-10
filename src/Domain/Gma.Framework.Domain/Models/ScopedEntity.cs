namespace Gma.Framework.Domain.Models;

using Gma.Framework.Naming;

public abstract class ScopedEntity<TId> : Entity<TId>, IScopedEntity
    where TId : notnull
{
    protected ScopedEntity() { }

    protected ScopedEntity(TId id, string scopeId)
        : base(id)
        => this.ScopeId = ScopeIds.Normalize(scopeId);

    public string ScopeId { get; private set; } = string.Empty;
}
