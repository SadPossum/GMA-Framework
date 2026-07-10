namespace Gma.Framework.AccessControl;

using Gma.Framework.Modules;
using Gma.Framework.Permissions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class AccessControlServiceCollectionExtensions
{
    public static IServiceCollection AddGmaAccessControl(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IAccessAuthorizationService, AccessAuthorizationService>();
        services.TryAddSingleton<IAccessScopeMatchOptionsResolver, DescriptorAccessScopeMatchOptionsResolver>();
        return services;
    }

    public static IServiceCollection AddGmaAccessControlPermissionPolicies(
        this IServiceCollection services,
        ModuleDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(descriptor);

        services.AddGmaAccessControl();

        foreach (ModulePermissionDescriptor permission in descriptor.GetPermissions())
        {
            AccessPermissionScopePolicyRegistration registration = new(permission);
            AccessPermissionScopePolicyRegistration? existing = services
                .Where(service => service.ServiceType == typeof(AccessPermissionScopePolicyRegistration))
                .Select(service => service.ImplementationInstance)
                .OfType<AccessPermissionScopePolicyRegistration>()
                .SingleOrDefault(candidate => string.Equals(
                    candidate.Permission.Value,
                    registration.Permission.Value,
                    StringComparison.Ordinal));

            if (existing is not null)
            {
                if (existing.MatchOptions != registration.MatchOptions)
                {
                    throw new InvalidOperationException(
                        $"Permission '{registration.Permission.Value}' has conflicting access-scope grant policies.");
                }

                continue;
            }

            services.AddSingleton(registration);
        }

        return services;
    }

    public static IServiceCollection AddSharedAccessControl(this IServiceCollection services) =>
        AddGmaAccessControl(services);
}
