namespace Gma.Framework.Administration.Cli;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Gma.Framework.Administration;

public static class DependencyInjection
{
    public static IServiceCollection AddSharedAdministrationCli(this IServiceCollection services) =>
        AddGmaAdministrationCli(services);

    public static IServiceCollection AddGmaAdministrationCli(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddGmaAdministration();
        services.TryAddSingleton<AdminCliGlobalOptions>();
        services.TryAddSingleton<AdminCliExecutor>();

        return services;
    }
}
