namespace Gma.Framework.Administration.AccessControl;

using Gma.Framework.AccessControl;
using Gma.Framework.Administration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGmaAccessControlAdministrationAuthorization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddGmaAccessControl();
        services.Replace(ServiceDescriptor.Scoped<IAdminAuthorizationService, AccessControlAdminAuthorizationService>());

        return services;
    }
}
