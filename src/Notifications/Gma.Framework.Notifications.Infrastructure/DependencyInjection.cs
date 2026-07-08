namespace Gma.Framework.Notifications.Infrastructure;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Notifications;
using Gma.Framework.Runtime.Infrastructure;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddUserNotificationsInfrastructure(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        NotificationsOptions notificationOptions = builder.Configuration
            .GetSection(NotificationsOptions.SectionName)
            .Get<NotificationsOptions>() ?? new NotificationsOptions();
        ValidateOptionsResult validation = new NotificationsOptionsValidator().Validate(name: null, notificationOptions);
        if (validation.Failed)
        {
            throw new OptionsValidationException(
                NotificationsOptions.SectionName,
                typeof(NotificationsOptions),
                validation.Failures);
        }

        builder.AddRuntimeInfrastructure();

        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(UserNotificationsInfrastructureMarker)))
        {
            return builder;
        }

        builder.Services.AddSingleton<UserNotificationsInfrastructureMarker>();
        builder.ProvideFeature(NotificationsCompositionFeatures.PublisherProvided("Gma.Framework.Notifications.Infrastructure"));
        builder.ProvideFeature(NotificationsCompositionFeatures.RequestQueueProvided("Gma.Framework.Notifications.Infrastructure"));
        if (notificationOptions.Enabled)
        {
            builder.ProvideFeature(NotificationsCompositionFeatures.LiveFeedProvided("Gma.Framework.Notifications.Infrastructure"));
        }

        builder.Services.AddMetrics();
        builder.Services
            .AddOptions<NotificationsOptions>()
            .Bind(builder.Configuration.GetSection(NotificationsOptions.SectionName))
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<NotificationsOptions>, NotificationsOptionsValidator>());
        builder.Services.TryAddSingleton<NotificationMetrics>();
        builder.Services.TryAddSingleton<InMemoryUserNotificationBus>();
        builder.Services.TryAddSingleton<IUserNotificationFeed>(
            provider => provider.GetRequiredService<InMemoryUserNotificationBus>());
        builder.Services.AddSingleton<IUserNotificationSink>(
            provider => provider.GetRequiredService<InMemoryUserNotificationBus>());
        builder.Services.TryAddScoped<IUserNotificationPublisher, UserNotificationPublisher>();
        builder.Services.TryAddScoped<UserNotificationRequestQueue>();
        builder.Services.TryAddScoped<IUserNotificationRequestQueue>(
            provider => provider.GetRequiredService<UserNotificationRequestQueue>());
        builder.Services.TryAddScoped<IUserNotificationRequestQueueFlusher>(
            provider => provider.GetRequiredService<UserNotificationRequestQueue>());

        return builder;
    }

    private sealed class UserNotificationsInfrastructureMarker;
}
