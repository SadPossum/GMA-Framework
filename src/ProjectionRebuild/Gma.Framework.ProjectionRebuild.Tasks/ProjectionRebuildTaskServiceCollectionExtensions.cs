namespace Gma.Framework.ProjectionRebuild.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Gma.Framework.ProjectionRebuild;

public static class ProjectionRebuildTaskServiceCollectionExtensions
{
    public static IServiceCollection AddProjectionRebuildTasks(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProjectionRebuild();
        services.TryAddScoped(typeof(TaskProjectionRebuildRunner<>));

        return services;
    }
}
