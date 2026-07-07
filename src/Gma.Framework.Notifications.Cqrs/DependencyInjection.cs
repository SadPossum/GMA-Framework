namespace Gma.Framework.Notifications.Cqrs;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Gma.Framework.Cqrs;
using Gma.Framework.Cqrs.Infrastructure;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Notifications;
using Gma.Framework.Notifications.Infrastructure;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddUserNotificationsCqrs(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddUserNotificationsInfrastructure();
        builder.AddCqrsInfrastructure();

        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(UserNotificationsCqrsRegistrationMarker)))
        {
            return builder;
        }

        builder.Services.AddSingleton<UserNotificationsCqrsRegistrationMarker>();
        builder.ProvideFeature(NotificationsCompositionFeatures.CqrsRequestFlushProvided("Gma.Framework.Notifications.Cqrs"));
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped(typeof(ICommandPipelineBehavior<,>), typeof(NotificationRequestCommandBehavior<,>)));
        builder.Services.MoveCommandUnitOfWorkBehaviorToEnd();

        return builder;
    }

    private sealed class UserNotificationsCqrsRegistrationMarker;
}
