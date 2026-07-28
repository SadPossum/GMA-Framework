namespace Gma.Framework.AccessControl.AspNetCore;

using Gma.Framework.AccessControl;
using Gma.Framework.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddGmaAccessControlAspNetCore(
        this IServiceCollection services,
        Action<AccessControlAspNetCoreOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddGmaAccessControl();
        services.AddSecuritySignalCore();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                ISecuritySignalDefinitionSource,
                AccessControlSecuritySignalDefinitions>());
        services.AddOptions<AccessControlAspNetCoreOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddScoped<IAccessHttpSubjectResolver, ClaimsAccessHttpSubjectResolver>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAccessHttpScopeResolver, DefaultAccessHttpScopeResolver>());
        return services;
    }

    public static IServiceCollection AddSharedAccessControlAspNetCore(
        this IServiceCollection services,
        Action<AccessControlAspNetCoreOptions>? configure = null) =>
        AddGmaAccessControlAspNetCore(services, configure);
}
