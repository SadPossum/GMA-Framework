namespace Gma.Framework.Realtime.Notifications;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Notifications;
using Gma.Framework.Realtime;
using Gma.Framework.Realtime.Infrastructure;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddUserNotificationsRealtime(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(UserNotificationsRealtimeMarker)))
        {
            return builder;
        }

        NotificationsOptions notificationOptions = builder.Configuration
            .GetSection(NotificationsOptions.SectionName)
            .Get<NotificationsOptions>() ?? new NotificationsOptions();
        if (notificationOptions.SubscriberQueueCapacity is <= 0 or > 10_000)
        {
            throw new OptionsValidationException(
                NotificationsOptions.SectionName,
                typeof(NotificationsOptions),
                ["Notifications:SubscriberQueueCapacity must be between 1 and 10000."]);
        }

        builder.Services.AddSingleton<UserNotificationsRealtimeMarker>();
        if (notificationOptions.Enabled)
        {
            builder.ProvideFeature(NotificationsCompositionFeatures.LiveFeedProvided("Gma.Framework.Realtime.Notifications"));
        }

        builder.Services
            .AddOptions<RealtimeSubscriberQueueOptions<UserNotificationMessage>>()
            .Configure(options => options.SubscriberQueueCapacity = notificationOptions.SubscriberQueueCapacity);
        builder.Services.AddInMemoryRealtimeBus<UserNotificationMessage>();
        builder.Services.TryAddSingleton<IUserNotificationFeed, UserNotificationRealtimeFeed>();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IUserNotificationSink, UserNotificationRealtimeSink>());

        return builder;
    }

    private sealed class UserNotificationsRealtimeMarker;
}
