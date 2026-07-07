namespace Gma.Framework.Notifications.SignalR;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Gma.Framework.Notifications;

public static class UserNotificationSignalREndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapUserNotificationSignalR(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        NotificationsOptions notificationsOptions = endpoints.ServiceProvider
            .GetRequiredService<IOptions<NotificationsOptions>>()
            .Value;
        NotificationSignalROptions signalROptions = endpoints.ServiceProvider
            .GetRequiredService<IOptions<NotificationSignalROptions>>()
            .Value;

        if (!notificationsOptions.Enabled || !signalROptions.Enabled)
        {
            return endpoints;
        }

        endpoints.MapHub<UserNotificationsHub>(signalROptions.HubPath)
            .RequireAuthorization();

        return endpoints;
    }
}
