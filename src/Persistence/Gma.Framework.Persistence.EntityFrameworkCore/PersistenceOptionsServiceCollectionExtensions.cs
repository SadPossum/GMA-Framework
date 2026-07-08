namespace Gma.Framework.Persistence.EntityFrameworkCore;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

public static class PersistenceOptionsServiceCollectionExtensions
{
    public static IServiceCollection AddPersistenceOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        if (services.Any(descriptor => descriptor.ServiceType == typeof(PersistenceOptionsRegistrationMarker)))
        {
            return services;
        }

        PersistenceOptionsValidation.GetValidatedOptions(configuration);
        services.AddSingleton<PersistenceOptionsRegistrationMarker>();
        services
            .AddOptions<PersistenceOptions>()
            .Bind(configuration.GetSection(PersistenceOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<PersistenceOptions>, PersistenceOptionsValidator>());

        return services;
    }

    private sealed class PersistenceOptionsRegistrationMarker;
}
