namespace Gma.Framework.Tenancy.Tasks;

using Gma.Framework.Tasks.Infrastructure;
using Gma.Framework.Tenancy;

internal sealed class TenantTaskExecutionContextContributor(ITenantContextAccessor tenantContext)
    : ITaskExecutionContextContributor
{
    public ValueTask<TaskExecutionContextPreparationResult> PrepareAsync(
        TaskExecutionContextPreparationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        tenantContext.ClearTenant();
        if (!context.Registration.IsTenantScoped())
        {
            return ValueTask.FromResult(TaskExecutionContextPreparationResult.Success());
        }

        if (string.IsNullOrWhiteSpace(context.Lease.ScopeId))
        {
            return ValueTask.FromResult(TaskExecutionContextPreparationResult.Failure(
                $"Scope-aware task {context.Lease.ModuleName}.{context.Lease.TaskName} has no scope id."));
        }

        tenantContext.SetTenant(context.Lease.ScopeId);

        return ValueTask.FromResult(TaskExecutionContextPreparationResult.Success());
    }

    public ValueTask CleanupAsync(
        TaskExecutionContextPreparationContext context,
        CancellationToken cancellationToken)
    {
        tenantContext.ClearTenant();
        return ValueTask.CompletedTask;
    }
}
