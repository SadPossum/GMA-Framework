namespace Gma.Framework.ProjectionRebuild.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore;
using Gma.Framework.Naming;

public abstract class EfProjectionRebuildCheckpointStore<TDbContext, TCheckpointState>(
    TDbContext dbContext,
    string moduleName,
    bool scopeAware,
    Func<TCheckpointState> createState)
    : IProjectionRebuildCheckpointStore
    where TDbContext : DbContext
    where TCheckpointState : ProjectionRebuildCheckpointState
{
    private readonly bool scopeAware = scopeAware;
    private readonly Func<TCheckpointState> createState =
        createState ?? throw new ArgumentNullException(nameof(createState));

    public string ModuleName { get; } = SharedModuleNames.Normalize(moduleName);

    public async Task<ProjectionRebuildCheckpoint?> GetAsync(
        ProjectionRebuildCheckpointKey key,
        CancellationToken cancellationToken)
    {
        string scopeValue = this.ValidateAndGetScope(key);

        TCheckpointState? checkpoint = await dbContext
            .Set<TCheckpointState>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item =>
                    item.RunId == key.RunId &&
                    item.ScopeId == scopeValue &&
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
        string scopeValue = this.ValidateAndGetScope(key);

        TCheckpointState? state = await dbContext
            .Set<TCheckpointState>()
            .SingleOrDefaultAsync(
                item =>
                    item.RunId == key.RunId &&
                    item.ScopeId == scopeValue &&
                    item.ProjectionName == key.ProjectionName,
                cancellationToken)
            .ConfigureAwait(false);

        if (state is null)
        {
            state = this.createState();
            state.Initialize(key, checkpoint, this.scopeAware);
            dbContext.Set<TCheckpointState>().Add(state);
        }
        else
        {
            state.Update(checkpoint);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private string ValidateAndGetScope(ProjectionRebuildCheckpointKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (!string.Equals(key.ModuleName, this.ModuleName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Projection checkpoint store for module '{this.ModuleName}' cannot handle module '{key.ModuleName}'.");
        }

        return ProjectionRebuildCheckpointState.NormalizeScope(key.ScopeId, this.scopeAware);
    }
}
