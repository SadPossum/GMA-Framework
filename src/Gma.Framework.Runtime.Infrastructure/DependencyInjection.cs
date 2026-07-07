namespace Gma.Framework.Runtime.Infrastructure;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Gma.Framework.Runtime;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Infrastructure.Identity;
using Gma.Framework.Runtime.Infrastructure.Time;
using Gma.Framework.Runtime.Time;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddRuntimeInfrastructure(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        ValidateApplicationIdentityOptions(builder.Configuration);

        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(RuntimeInfrastructureRegistrationMarker)))
        {
            return builder;
        }

        builder.Services.AddSingleton<RuntimeInfrastructureRegistrationMarker>();
        builder.Services
            .AddOptions<ApplicationIdentityOptions>()
            .Bind(builder.Configuration.GetSection(ApplicationIdentityOptions.SectionName))
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<ApplicationIdentityOptions>, ApplicationIdentityOptionsValidator>());
        builder.Services.TryAddSingleton<IIdGenerator, GuidIdGenerator>();
        builder.Services.TryAddSingleton<ISystemClock, SystemClock>();

        return builder;
    }

    private static void ValidateApplicationIdentityOptions(IConfiguration configuration)
    {
        ApplicationIdentityOptions options = configuration
            .GetSection(ApplicationIdentityOptions.SectionName)
            .Get<ApplicationIdentityOptions>() ?? new ApplicationIdentityOptions();
        ValidateOptionsResult result = new ApplicationIdentityOptionsValidator().Validate(name: null, options);

        if (result.Failed)
        {
            throw new OptionsValidationException(
                ApplicationIdentityOptions.SectionName,
                typeof(ApplicationIdentityOptions),
                result.Failures);
        }
    }

    private sealed class RuntimeInfrastructureRegistrationMarker;
}
