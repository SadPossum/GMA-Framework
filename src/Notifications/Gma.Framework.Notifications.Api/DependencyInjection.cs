namespace Gma.Framework.Notifications.Api;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Notifications;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddUserNotificationServerSentEvents(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        NotificationSseOptions sseOptions = builder.Configuration
            .GetSection(NotificationSseOptions.SectionName)
            .Get<NotificationSseOptions>() ?? new NotificationSseOptions();
        ValidateOptionsResult validation = new NotificationSseOptionsValidator().Validate(name: null, sseOptions);
        if (validation.Failed)
        {
            throw new OptionsValidationException(
                NotificationSseOptions.SectionName,
                typeof(NotificationSseOptions),
                validation.Failures);
        }

        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(NotificationSseRegistrationMarker)))
        {
            return builder;
        }

        builder.Services.AddSingleton<NotificationSseRegistrationMarker>();
        NotificationsOptions notificationsOptions = builder.Configuration
            .GetSection(NotificationsOptions.SectionName)
            .Get<NotificationsOptions>() ?? new NotificationsOptions();
        if (notificationsOptions.Enabled && sseOptions.Enabled)
        {
            builder.ProvideFeature(NotificationsCompositionFeatures.ServerSentEventsProvided("Gma.Framework.Notifications.Api"));
            builder.RequireFeature(NotificationsCompositionFeatures.LiveFeedRequired(
                "Gma.Framework.Notifications.Api",
                "Register Gma.Framework.Realtime.Notifications before enabling notification SSE streaming."));
        }

        builder.Services
            .AddOptions<NotificationsOptions>()
            .Bind(builder.Configuration.GetSection(NotificationsOptions.SectionName))
            .ValidateOnStart();
        builder.Services
            .AddOptions<NotificationSseOptions>()
            .Bind(builder.Configuration.GetSection(NotificationSseOptions.SectionName))
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<NotificationSseOptions>, NotificationSseOptionsValidator>());

        return builder;
    }

    private sealed class NotificationSseRegistrationMarker;
}
