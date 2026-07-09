namespace Gma.Framework.Realtime.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

public static class DependencyInjection
{
    public static IServiceCollection AddInMemoryRealtimeBus<TMessage>(this IServiceCollection services)
        where TMessage : notnull
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddOptions<RealtimeSubscriberQueueOptions<TMessage>>()
            .ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IValidateOptions<RealtimeSubscriberQueueOptions<TMessage>>,
                RealtimeSubscriberQueueOptionsValidator<TMessage>>());
        services.TryAddSingleton<InMemoryRealtimeBus<TMessage>>();
        services.TryAddSingleton<IRealtimeFeed<TMessage>>(
            provider => provider.GetRequiredService<InMemoryRealtimeBus<TMessage>>());
        services.TryAddSingleton<IRealtimeSink<TMessage>>(
            provider => provider.GetRequiredService<InMemoryRealtimeBus<TMessage>>());

        return services;
    }
}
