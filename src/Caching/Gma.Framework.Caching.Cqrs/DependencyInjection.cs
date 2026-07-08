namespace Gma.Framework.Caching.Cqrs;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Gma.Framework.Caching;
using Gma.Framework.Caching.Infrastructure;
using Gma.Framework.Cqrs;
using Gma.Framework.Cqrs.Infrastructure;
using Gma.Framework.ModuleComposition;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddCachingCqrs(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddCachingInfrastructure();
        builder.AddCqrsInfrastructure();

        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(CachingCqrsRegistrationMarker)))
        {
            return builder;
        }

        builder.Services.AddSingleton<CachingCqrsRegistrationMarker>();
        builder.ProvideFeature(CachingCompositionFeatures.CqrsInvalidationProvided("Gma.Framework.Caching.Cqrs"));
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped(typeof(ICommandPipelineBehavior<,>), typeof(CacheInvalidationCommandBehavior<,>)));
        builder.Services.MoveCommandUnitOfWorkBehaviorToEnd();

        return builder;
    }

    private sealed class CachingCqrsRegistrationMarker;
}
