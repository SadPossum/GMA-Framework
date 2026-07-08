namespace Gma.Framework.Domain.Models;

using Gma.Framework.Naming;

public abstract class TenantAggregateRoot<TId> : AggregateRoot<TId>, ITenantScoped
    where TId : notnull
{
    protected TenantAggregateRoot() { }

    protected TenantAggregateRoot(TId id, string tenantId)
        : base(id)
        => this.TenantId = TenantIds.Normalize(tenantId);

    public string TenantId { get; private set; } = string.Empty;
}
