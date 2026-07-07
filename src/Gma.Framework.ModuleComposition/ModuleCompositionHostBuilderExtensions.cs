namespace Gma.Framework.ModuleComposition;

using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public static class ModuleCompositionHostBuilderExtensions
{
    public static IHostApplicationBuilder AddModuleComposition(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder;
    }

    public static IHostApplicationBuilder SelectModuleProfile(
        this IHostApplicationBuilder builder,
        ModuleProfileDescriptor profile,
        string? selectedBy = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(profile);

        builder.Services.AddSingleton(new SelectedModuleProfile(profile, selectedBy));
        return builder;
    }

    public static IHostApplicationBuilder ProvideFeature(
        this IHostApplicationBuilder builder,
        ProvidedCompositionFeature feature)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(feature);

        builder.Services.AddSingleton(feature);
        return builder;
    }

    public static IHostApplicationBuilder RequireFeature(
        this IHostApplicationBuilder builder,
        RequiredCompositionFeature feature)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(feature);

        builder.Services.AddSingleton(feature);
        return builder;
    }

    public static IHostApplicationBuilder RequireModule(
        this IHostApplicationBuilder builder,
        RequiredCompositionModule module)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(module);

        builder.Services.AddSingleton(module);
        return builder;
    }

    public static ModuleCompositionValidationResult ValidateModuleComposition(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        ModuleCompositionValidationResult result = ModuleCompositionValidator.Validate(CreateSnapshot(builder.Services));
        if (!result.IsValid)
        {
            throw new ModuleCompositionValidationException(result.Errors, result.Report);
        }

        return result;
    }

    public static ModuleCompositionValidationResult ValidateModuleComposition(this IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        ModuleCompositionSnapshot snapshot = new(
            serviceProvider.GetServices<SelectedModuleProfile>().ToArray(),
            serviceProvider.GetServices<ProvidedCompositionFeature>().ToArray(),
            serviceProvider.GetServices<RequiredCompositionFeature>().ToArray(),
            serviceProvider.GetServices<RequiredCompositionModule>().ToArray());
        ModuleCompositionValidationResult result = ModuleCompositionValidator.Validate(snapshot);
        if (!result.IsValid)
        {
            throw new ModuleCompositionValidationException(result.Errors, result.Report);
        }

        return result;
    }

    private static ModuleCompositionSnapshot CreateSnapshot(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return new(
            GetImplementationInstances<SelectedModuleProfile>(services),
            GetImplementationInstances<ProvidedCompositionFeature>(services),
            GetImplementationInstances<RequiredCompositionFeature>(services),
            GetImplementationInstances<RequiredCompositionModule>(services));
    }

    private static ReadOnlyCollection<T> GetImplementationInstances<T>(IServiceCollection services)
        where T : class =>
        Array.AsReadOnly(services
            .Where(descriptor => descriptor.ServiceType == typeof(T))
            .Select(descriptor => descriptor.ImplementationInstance)
            .OfType<T>()
            .ToArray());
}
