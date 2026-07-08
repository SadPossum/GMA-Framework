namespace Gma.Framework.ProjectionRebuild.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore;
using Gma.Framework.Naming;

public abstract class EfProjectionRebuildCheckpointStore<TDbContext, TCheckpointState>(
    TDbContext dbContext,
    string moduleName,
    bool tenantScoped,
    Func<TCheckpointState> createState)
    : IProjectionRebuildCheckpointStore
    where TDbContext : DbContext
    where TCheckpointState : ProjectionRebuildCheckpointState
{
    private readonly bool tenantScoped = tenantScoped;
    private readonly Func<TCheckpointState> createState =
        createState ?? throw new ArgumentNullException(nameof(createState));

    public string ModuleName { get; } = SharedModuleNames.Normalize(moduleName);

    public async Task<ProjectionRebuildCheckpoint?> GetAsync(
        ProjectionRebuildCheckpointKey key,
        CancellationToken cancellationToken)
    {
        string tenantScope = this.ValidateAndGetTenantScope(key);

        TCheckpointState? checkpoint = await dbContext
            .Set<TCheckpointState>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item =>
                    item.RunId == key.RunId &&
                    item.TenantId == tenantScope &&
                    item.ProjectionName == key.ProjectionName,
                cancellationToken)
            .ConfigureAwait(false);

        return checkpoint?.ToCheckpoint();
    }

    public async Task SaveAsync(
        ProjectionRebuildCheckpointKey key,
        ProjectionRebuildCheckpoint checkpoint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        string tenantScope = this.ValidateAndGetTenantScope(key);

        TCheckpointState? state = await dbContext
            .Set<TCheckpointState>()
            .SingleOrDefaultAsync(
                item =>
                    item.RunId == key.RunId &&
                    item.TenantId == tenantScope &&
                    item.ProjectionName == key.ProjectionName,
                cancellationToken)
            .ConfigureAwait(false);

        if (state is null)
        {
            state = this.createState();
            state.Initialize(key, checkpoint, this.tenantScoped);
            dbContext.Set<TCheckpointState>().Add(state);
        }
        else
        {
            state.Update(checkpoint);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private string ValidateAndGetTenantScope(ProjectionRebuildCheckpointKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (!string.Equals(key.ModuleName, this.ModuleName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Projection checkpoint store for module '{this.ModuleName}' cannot handle module '{key.ModuleName}'.");
        }

        return ProjectionRebuildCheckpointState.NormalizeTenantScope(key.TenantId, this.tenantScoped);
    }
}
