namespace Gma.Framework.AccessControl;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class AccessControlServiceCollectionExtensions
{
    public static IServiceCollection AddGmaAccessControl(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IAccessAuthorizationService, AccessAuthorizationService>();
        return services;
    }

    public static IServiceCollection AddSharedAccessControl(this IServiceCollection services) =>
        AddGmaAccessControl(services);
}
