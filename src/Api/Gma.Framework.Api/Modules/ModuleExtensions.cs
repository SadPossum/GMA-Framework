namespace Gma.Framework.Api.Modules;

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Gma.Framework.Naming;

public static class ModuleExtensions
{
    public static IHostApplicationBuilder AddModule<TModule>(this IHostApplicationBuilder builder)
        where TModule : IModule, new()
    {
        ArgumentNullException.ThrowIfNull(builder);

        return HasModule(builder.Services, typeof(TModule))
            ? builder
            : AddModule(builder, new TModule());
    }

    public static IHostApplicationBuilder AddModule(this IHostApplicationBuilder builder, IModule module)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(module);

        if (HasModule(builder.Services, module.GetType()))
        {
            return builder;
        }

        string moduleName = NormalizeModuleName(module.Name, module.GetType(), nameof(IModule));
        EnsureModuleNameAvailable(builder.Services, moduleName, module.GetType());

        module.AddServices(builder);
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IModule>(module));

        return builder;
    }

    public static IEndpointRouteBuilder MapModules(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        IModule[] modules = GetRequiredModules(endpoints.ServiceProvider);

        foreach (IModule module in modules)
        {
            module.MapEndpoints(endpoints);
        }

        return endpoints;
    }

    internal static IModule[] GetRequiredModules(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        return ValidateModules(serviceProvider.GetServices<IModule>());
    }

    internal static IModule[] ValidateModules(IEnumerable<IModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        ModuleRegistration[] registrations = modules
            .Select(CreateRegistration)
            .ToArray();

        IGrouping<string, ModuleRegistration>? duplicate = registrations
            .GroupBy(registration => registration.Name, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"{duplicate.Count()} API modules are registered with name '{duplicate.Key}'.");
        }

        return registrations.Select(registration => registration.Module).ToArray();
    }

    private static ModuleRegistration CreateRegistration(IModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        string normalizedName = NormalizeModuleName(module.Name, module.GetType(), nameof(IModule));
        return new(normalizedName, module);
    }

    private static string NormalizeModuleName(string moduleName, Type moduleType, string moduleKind)
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
                $"{moduleKind} '{moduleType.FullName}' has an invalid name.",
                exception);
        }
    }

    private sealed record ModuleRegistration(string Name, IModule Module);

    private static void EnsureModuleNameAvailable(IServiceCollection services, string moduleName, Type moduleType)
    {
        foreach (ServiceDescriptor descriptor in services.Where(descriptor => descriptor.ServiceType == typeof(IModule)))
        {
            if (descriptor.ImplementationInstance is not IModule module)
            {
                continue;
            }

            string existingName = NormalizeModuleName(module.Name, module.GetType(), nameof(IModule));
            if (string.Equals(existingName, moduleName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"API module '{moduleType.FullName}' cannot be registered because module name '{moduleName}' is already registered by '{module.GetType().FullName}'.");
            }
        }
    }

    private static bool HasModule(IServiceCollection services, Type moduleType) =>
        services.Any(descriptor =>
            descriptor.ServiceType == typeof(IModule) &&
            (descriptor.ImplementationType == moduleType ||
             descriptor.ImplementationInstance?.GetType() == moduleType));
}
