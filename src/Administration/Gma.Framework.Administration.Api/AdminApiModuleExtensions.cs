namespace Gma.Framework.Administration.Api;

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Gma.Framework.Naming;

public static class AdminApiModuleExtensions
{
    public static IHostApplicationBuilder AddAdminApiModule<TModule>(this IHostApplicationBuilder builder)
        where TModule : IAdminApiModule, new()
    {
        ArgumentNullException.ThrowIfNull(builder);

        return HasModule(builder.Services, typeof(TModule))
            ? builder
            : AddAdminApiModule(builder, new TModule());
    }

    public static IHostApplicationBuilder AddAdminApiModule(this IHostApplicationBuilder builder, IAdminApiModule module)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(module);

        if (HasModule(builder.Services, module.GetType()))
        {
            return builder;
        }

        string moduleName = NormalizeModuleName(module.Name, module.GetType());
        EnsureModuleNameAvailable(builder.Services, moduleName, module.GetType());

        module.AddServices(builder);
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IAdminApiModule>(module));

        return builder;
    }

    public static IEndpointRouteBuilder MapAdminApiModules(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        foreach (IAdminApiModule module in GetRequiredModules(endpoints.ServiceProvider))
        {
            module.MapEndpoints(endpoints);
        }

        return endpoints;
    }

    internal static IAdminApiModule[] GetRequiredModules(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        return ValidateModules(serviceProvider.GetServices<IAdminApiModule>());
    }

    internal static IAdminApiModule[] ValidateModules(IEnumerable<IAdminApiModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        AdminApiModuleRegistration[] registrations = modules
            .Select(CreateRegistration)
            .ToArray();

        IGrouping<string, AdminApiModuleRegistration>? duplicate = registrations
            .GroupBy(registration => registration.Name, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"{duplicate.Count()} admin API modules are registered with name '{duplicate.Key}'.");
        }

        return registrations.Select(registration => registration.Module).ToArray();
    }

    private static AdminApiModuleRegistration CreateRegistration(IAdminApiModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        string normalizedName = NormalizeModuleName(module.Name, module.GetType());
        return new(normalizedName, module);
    }

    private static string NormalizeModuleName(string moduleName, Type moduleType)
    {
        try
        {
            string normalized = SharedModuleNames.Normalize(moduleName, "Name");
            if (!string.Equals(moduleName, normalized, StringComparison.Ordinal))
            {
                throw new ArgumentException("Module names must already be lowercase kebab-case and must not require normalization.");
            }

            return normalized;
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                $"Admin API module '{moduleType.FullName}' has an invalid name.",
                exception);
        }
    }

    private sealed record AdminApiModuleRegistration(string Name, IAdminApiModule Module);

    private static void EnsureModuleNameAvailable(IServiceCollection services, string moduleName, Type moduleType)
    {
        foreach (ServiceDescriptor descriptor in services.Where(descriptor => descriptor.ServiceType == typeof(IAdminApiModule)))
        {
            if (descriptor.ImplementationInstance is not IAdminApiModule module)
            {
                continue;
            }

            string existingName = NormalizeModuleName(module.Name, module.GetType());
            if (string.Equals(existingName, moduleName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Admin API module '{moduleType.FullName}' cannot be registered because module name '{moduleName}' is already registered by '{module.GetType().FullName}'.");
            }
        }
    }

    private static bool HasModule(IServiceCollection services, Type moduleType) =>
        services.Any(descriptor =>
            descriptor.ServiceType == typeof(IAdminApiModule) &&
            (descriptor.ImplementationType == moduleType ||
             descriptor.ImplementationInstance?.GetType() == moduleType));
}
