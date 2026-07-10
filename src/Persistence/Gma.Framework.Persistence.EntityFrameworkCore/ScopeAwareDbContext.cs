namespace Gma.Framework.Persistence.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore;
using Gma.Framework.Scoping;

public abstract class ScopeAwareDbContext<TContext>(
    DbContextOptions<TContext> options,
    IScopeContext scopeContext) : DbContext(options)
    where TContext : DbContext
{
    private readonly IScopeContext scopeContext = scopeContext;

    public bool ScopeFilterEnabled { get; } = scopeContext.IsEnabled;
    public string CurrentScopeId { get; } = scopeContext.ScopeId ?? string.Empty;

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        this.ChangeTracker.ValidateScopedWrites(this.scopeContext);
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        this.ChangeTracker.ValidateScopedWrites(this.scopeContext);
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected void ApplyScopeConventions(ModelBuilder modelBuilder)
        => modelBuilder.ApplyScopeConventions(this);
}
