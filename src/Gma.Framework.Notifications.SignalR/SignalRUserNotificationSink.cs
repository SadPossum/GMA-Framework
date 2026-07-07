namespace Gma.Framework.Notifications.SignalR;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Gma.Framework.Notifications;
using Gma.Framework.Runtime;

internal sealed class SignalRUserNotificationSink(
    IHubContext<UserNotificationsHub> hubContext,
    IOptions<ApplicationIdentityOptions> applicationIdentity,
    IOptions<NotificationSignalROptions> options) : IUserNotificationSink
{
    public string ProviderName => "signalr";

    public async ValueTask DeliverAsync(UserNotificationMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        if (!options.Value.Enabled)
        {
            return;
        }

        string groupName = NotificationSignalRGroupNames.ForUser(
            applicationIdentity.Value.EffectiveNamespace,
            message.TenantId,
            message.UserId);
        await hubContext.Clients
            .Group(groupName)
            .SendAsync(options.Value.ClientMethodName, message, cancellationToken)
            .ConfigureAwait(false);
    }
}
