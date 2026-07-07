namespace Gma.Framework.ProjectionRebuild;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class ProjectionRebuildServiceCollectionExtensions
{
    public static IServiceCollection AddProjectionRebuild(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IProjectionRebuildCheckpointStoreRegistry, ProjectionRebuildCheckpointStoreRegistry>();
        services.TryAddScoped<IProjectionRebuildTransactionBoundaryRegistry, ProjectionRebuildTransactionBoundaryRegistry>();
        services.TryAddScoped(typeof(ProjectionRebuildRunner<>));
        services.TryAddSingleton<ProjectionRebuildMetrics>();

        return services;
    }
}
