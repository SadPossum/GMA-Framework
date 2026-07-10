namespace Gma.Framework.Notifications.SignalR;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Notifications;
using Gma.Framework.Scoping;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddUserNotificationSignalR(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        NotificationSignalROptions signalROptions = builder.Configuration
            .GetSection(NotificationSignalROptions.SectionName)
            .Get<NotificationSignalROptions>() ?? new NotificationSignalROptions();
        ValidateOptionsResult validation = new NotificationSignalROptionsValidator().Validate(name: null, signalROptions);
        if (validation.Failed)
        {
            throw new OptionsValidationException(
                NotificationSignalROptions.SectionName,
                typeof(NotificationSignalROptions),
                validation.Failures);
        }

        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(NotificationSignalRRegistrationMarker)))
        {
            return builder;
        }

        builder.Services.AddSingleton<NotificationSignalRRegistrationMarker>();
        NotificationsOptions notificationsOptions = builder.Configuration
            .GetSection(NotificationsOptions.SectionName)
            .Get<NotificationsOptions>() ?? new NotificationsOptions();
        if (notificationsOptions.Enabled && signalROptions.Enabled)
        {
            builder.ProvideFeature(NotificationsCompositionFeatures.SignalRProvided("Gma.Framework.Notifications.SignalR"));
            builder.RequireFeature(ScopeCompositionFeatures.ContextRequired(
                "Gma.Framework.Notifications.SignalR",
                "Register scoping infrastructure before enabling notification SignalR delivery."));
            builder.RequireFeature(NotificationsCompositionFeatures.PublisherRequired(
                "Gma.Framework.Notifications.SignalR",
                "Register Gma.Framework.Notifications.Infrastructure before enabling notification SignalR delivery."));
        }

        builder.Services
            .AddOptions<NotificationsOptions>()
            .Bind(builder.Configuration.GetSection(NotificationsOptions.SectionName))
            .ValidateOnStart();
        builder.Services
            .AddOptions<NotificationSignalROptions>()
            .Bind(builder.Configuration.GetSection(NotificationSignalROptions.SectionName))
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<NotificationSignalROptions>, NotificationSignalROptionsValidator>());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IPostConfigureOptions<JwtBearerOptions>, NotificationSignalRJwtBearerPostConfigureOptions>());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IUserNotificationSink, SignalRUserNotificationSink>());
        builder.Services.AddSignalR();

        return builder;
    }

    private sealed class NotificationSignalRRegistrationMarker;
}
