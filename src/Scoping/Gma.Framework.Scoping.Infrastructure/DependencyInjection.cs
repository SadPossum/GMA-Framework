namespace Gma.Framework.Scoping.Infrastructure;

using Gma.Framework.ModuleComposition;
using Gma.Framework.Scoping;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddScopingInfrastructure(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ProvideFeature(ScopeCompositionFeatures.ContextProvided("Gma.Framework.Scoping.Infrastructure"));

        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(ScopingInfrastructureRegistrationMarker)))
        {
            return builder;
        }

        ValidateOptionsResult validation = new ScopeOptionsValidator().Validate(
            name: null,
            builder.Configuration.GetSection(ScopeOptions.SectionName).Get<ScopeOptions>() ?? new ScopeOptions());
        if (validation.Failed)
        {
            throw new OptionsValidationException(
                ScopeOptions.SectionName,
                typeof(ScopeOptions),
                validation.Failures);
        }

        builder.Services.AddSingleton<ScopingInfrastructureRegistrationMarker>();
        builder.Services
            .AddOptions<ScopeOptions>()
            .Bind(builder.Configuration.GetSection(ScopeOptions.SectionName))
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<ScopeOptions>, ScopeOptionsValidator>());
        builder.Services.TryAddScoped<DefaultScopeContext>();
        builder.Services.TryAddScoped<IScopeContext>(provider => provider.GetRequiredService<DefaultScopeContext>());
        builder.Services.TryAddScoped<IScopeContextAccessor>(provider => provider.GetRequiredService<DefaultScopeContext>());

        return builder;
    }

    private sealed class ScopingInfrastructureRegistrationMarker;
}
