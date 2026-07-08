namespace Gma.Framework.Tenancy.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Gma.Framework.ModuleComposition;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddTenancyInfrastructure(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ProvideFeature(TenancyCompositionFeatures.ContextProvided("Gma.Framework.Tenancy.Infrastructure"));

        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(TenancyInfrastructureRegistrationMarker)))
        {
            return builder;
        }

        TenantOptions tenantOptions = builder.Configuration
            .GetSection(TenantOptions.SectionName)
            .Get<TenantOptions>() ?? new TenantOptions();
        ValidateOptionsResult tenantValidation = new TenantOptionsValidator().Validate(name: null, tenantOptions);
        if (tenantValidation.Failed)
        {
            throw new OptionsValidationException(
                TenantOptions.SectionName,
                typeof(TenantOptions),
                tenantValidation.Failures);
        }

        builder.Services.AddSingleton<TenancyInfrastructureRegistrationMarker>();
        builder.Services
            .AddOptions<TenantOptions>()
            .Bind(builder.Configuration.GetSection(TenantOptions.SectionName))
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<TenantOptions>, TenantOptionsValidator>());
        builder.Services.TryAddScoped<NullTenantContext>();
        builder.Services.TryAddScoped<ITenantContext>(provider => provider.GetRequiredService<NullTenantContext>());
        builder.Services.TryAddScoped<ITenantContextAccessor>(provider => provider.GetRequiredService<NullTenantContext>());

        return builder;
    }

    private sealed class TenancyInfrastructureRegistrationMarker;
}
