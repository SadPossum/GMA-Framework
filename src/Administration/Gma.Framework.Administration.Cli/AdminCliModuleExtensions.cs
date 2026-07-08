namespace Gma.Framework.Administration.Cli;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Gma.Framework.Naming;
using Gma.Framework.Runtime;
using System.CommandLine;

public static class AdminCliModuleExtensions
{
    public static IHostApplicationBuilder AddAdminModule<TModule>(this IHostApplicationBuilder builder)
        where TModule : IAdminCliModule, new()
    {
        ArgumentNullException.ThrowIfNull(builder);

        return HasModule(builder.Services, typeof(TModule))
            ? builder
            : AddAdminModule(builder, new TModule());
    }

    public static IHostApplicationBuilder AddAdminModule(this IHostApplicationBuilder builder, IAdminCliModule module)
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
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IAdminCliModule>(module));

        return builder;
    }

    public static RootCommand CreateAdminRootCommand(this IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        AdminCliGlobalOptions options = serviceProvider.GetRequiredService<AdminCliGlobalOptions>();
        string displayName = serviceProvider.GetService<IOptions<ApplicationIdentityOptions>>()?.Value.EffectiveDisplayName ??
                             ApplicationIdentityOptions.DefaultDisplayName;
        RootCommand rootCommand = new($"{displayName} administration CLI")
        {
            options.ActorOption,
            options.TenantOption,
            options.OutputOption
        };
        AdminCliCommandRegistry registry = new(rootCommand, serviceProvider);

        foreach (IAdminCliModule module in GetRequiredModules(serviceProvider))
        {
            module.MapCommands(registry);
        }

        return rootCommand;
    }

    public static IServiceProvider ValidateAdminCliStartup(this IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        serviceProvider.GetService<IStartupValidator>()?.Validate();
        return serviceProvider;
    }

    internal static IAdminCliModule[] GetRequiredModules(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        return ValidateModules(serviceProvider.GetServices<IAdminCliModule>());
    }

    internal static IAdminCliModule[] ValidateModules(IEnumerable<IAdminCliModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        AdminCliModuleRegistration[] registrations = modules
            .Select(CreateRegistration)
            .ToArray();

        IGrouping<string, AdminCliModuleRegistration>? duplicate = registrations
            .GroupBy(registration => registration.Name, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"{duplicate.Count()} admin CLI modules are registered with name '{duplicate.Key}'.");
        }

        return registrations.Select(registration => registration.Module).ToArray();
    }

    private static AdminCliModuleRegistration CreateRegistration(IAdminCliModule module)
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
                $"Admin CLI module '{moduleType.FullName}' has an invalid name.",
                exception);
        }
    }

    private sealed record AdminCliModuleRegistration(string Name, IAdminCliModule Module);

    private static void EnsureModuleNameAvailable(IServiceCollection services, string moduleName, Type moduleType)
    {
        foreach (ServiceDescriptor descriptor in services.Where(descriptor => descriptor.ServiceType == typeof(IAdminCliModule)))
        {
            if (descriptor.ImplementationInstance is not IAdminCliModule module)
            {
                continue;
            }

            string existingName = NormalizeModuleName(module.Name, module.GetType());
            if (string.Equals(existingName, moduleName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Admin CLI module '{moduleType.FullName}' cannot be registered because module name '{moduleName}' is already registered by '{module.GetType().FullName}'.");
            }
        }
    }

    private static bool HasModule(IServiceCollection services, Type moduleType) =>
        services.Any(descriptor =>
            descriptor.ServiceType == typeof(IAdminCliModule) &&
            (descriptor.ImplementationType == moduleType ||
             descriptor.ImplementationInstance?.GetType() == moduleType));
}
