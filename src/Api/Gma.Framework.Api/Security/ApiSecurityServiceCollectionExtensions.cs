namespace Gma.Framework.Api.Security;

using Microsoft.Extensions.DependencyInjection;

public static class ApiSecurityServiceCollectionExtensions
{
    public static IServiceCollection AddApiSecurityDefaults(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAuthentication();
        services.AddAuthorization();

        return services;
    }

    public static IServiceCollection AddGmaApiSecurityDefaults(this IServiceCollection services) =>
        AddApiSecurityDefaults(services);
}
