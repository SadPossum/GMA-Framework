namespace Gma.Framework.Persistence.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore;
using Gma.Framework.Tenancy;

public abstract class TenantAwareDbContext<TContext>(
    DbContextOptions<TContext> options,
    ITenantContext tenantContext) : DbContext(options)
    where TContext : DbContext
{
    private readonly ITenantContext tenantContext = tenantContext;

    public bool TenantFilterEnabled { get; } = tenantContext.IsEnabled;
    public string CurrentTenantId { get; } = tenantContext.TenantId ?? string.Empty;

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        this.ChangeTracker.ValidateTenantScopedWrites(this.tenantContext);
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        this.ChangeTracker.ValidateTenantScopedWrites(this.tenantContext);
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected void ApplyTenantConventions(ModelBuilder modelBuilder)
        => modelBuilder.ApplyTenantConventions(this);
}
