namespace Gma.Framework.Administration.Api;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Gma.Framework.Administration;

public static class DependencyInjection
{
    public static IServiceCollection AddSharedAdministrationApi(
        this IServiceCollection services,
        IConfiguration configuration) =>
        AddGmaAdministrationApi(services, configuration);

    public static IServiceCollection AddGmaAdministrationApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        AdminApiOptions options = configuration
            .GetSection(AdminApiOptions.SectionName)
            .Get<AdminApiOptions>() ?? new AdminApiOptions();
        ValidateAdminApiOptions(options);

        services.AddGmaAdministration();
        services.TryAddScoped<AdminApiExecutor>();

        if (!services.Any(descriptor => descriptor.ServiceType == typeof(AdminApiOptionsRegistrationMarker)))
        {
            services.AddSingleton<AdminApiOptionsRegistrationMarker>();
            services
                .AddOptions<AdminApiOptions>()
                .Bind(configuration.GetSection(AdminApiOptions.SectionName))
                .ValidateOnStart();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<AdminApiOptions>, AdminApiOptionsValidator>());
        }

        return services;
    }

    private static void ValidateAdminApiOptions(AdminApiOptions options)
    {
        ValidateOptionsResult result = new AdminApiOptionsValidator().Validate(name: null, options);

        if (result.Failed)
        {
            throw new OptionsValidationException(AdminApiOptions.SectionName, typeof(AdminApiOptions), result.Failures);
        }
    }

    private sealed class AdminApiOptionsRegistrationMarker;
}
