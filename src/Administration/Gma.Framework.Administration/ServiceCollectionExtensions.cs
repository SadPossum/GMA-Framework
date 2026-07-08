namespace Gma.Framework.Administration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSharedAdministration(this IServiceCollection services) =>
        AddGmaAdministration(services);

    public static IServiceCollection AddGmaAdministration(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<ScopedAdminActorContext>();
        services.TryAddScoped<IAdminActorContext>(provider => provider.GetRequiredService<ScopedAdminActorContext>());
        services.TryAddScoped<IAdminActorContextAccessor>(provider => provider.GetRequiredService<ScopedAdminActorContext>());
        services.TryAddScoped<IAdminAuthorizationService, DenyAllAdminAuthorizationService>();
        services.TryAddScoped<IAdminAuditSink, NullAdminAuditSink>();
        services.TryAddScoped<IAdminOperationRunner, AdminOperationRunner>();

        return services;
    }
}
