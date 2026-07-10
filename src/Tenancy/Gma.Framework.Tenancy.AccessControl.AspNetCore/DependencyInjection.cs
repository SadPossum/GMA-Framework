namespace Gma.Framework.Tenancy.AccessControl.AspNetCore;

using Gma.Framework.AccessControl.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddGmaTenantAccessControlAspNetCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddGmaAccessControlAspNetCore();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAccessHttpScopeResolver, TenantAccessScopeResolver>());

        return services;
    }

    public static IServiceCollection AddSharedTenantAccessControlAspNetCore(this IServiceCollection services) =>
        AddGmaTenantAccessControlAspNetCore(services);
}
