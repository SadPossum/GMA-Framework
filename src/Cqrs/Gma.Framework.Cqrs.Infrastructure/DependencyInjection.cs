namespace Gma.Framework.Cqrs.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Gma.Framework.Cqrs;
using Gma.Framework.Observability.Infrastructure;
using Gma.Framework.Runtime.Infrastructure;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddCqrsInfrastructure(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddRuntimeInfrastructure();

        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(CqrsInfrastructureRegistrationMarker)))
        {
            return builder;
        }

        builder.Services.AddSingleton<CqrsInfrastructureRegistrationMarker>();
        builder.Services
            .AddOptions<CommandOutcomeObservationOptions>()
            .Bind(builder.Configuration.GetSection(CommandOutcomeObservationOptions.SectionName))
            .Validate(
                CommandOutcomeObservationOptions.IsValid,
                $"Cqrs outcome-observation timeout must be between {CommandOutcomeObservationOptions.MinimumTimeout} and {CommandOutcomeObservationOptions.MaximumTimeout}.")
            .ValidateOnStart();
        builder.Services.AddMetrics();
        builder.Services.TryAddScoped<IRequestDispatcher, RequestDispatcher>();
        builder.Services.TryAddSingleton<CommandMetrics>();
        builder.Services.TryAddSingleton<QueryMetrics>();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(ICommandPipelineBehavior<,>), typeof(ValidationCommandBehavior<,>)));
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(ICommandPipelineBehavior<,>), typeof(LoggingCommandBehavior<,>)));
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(ICommandPipelineBehavior<,>), typeof(CommandOutcomeObservationBehavior<,>)));
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(ICommandPipelineBehavior<,>), typeof(CommandUnitOfWorkBehavior<,>)));
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IQueryPipelineBehavior<,>), typeof(ValidationQueryBehavior<,>)));
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IQueryPipelineBehavior<,>), typeof(LoggingQueryBehavior<,>)));

        return builder;
    }

    private sealed class CqrsInfrastructureRegistrationMarker;
}
