namespace Gma.Framework.Notifications.SignalR;

using Gma.Framework.Notifications;
using Gma.Framework.Runtime;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

internal sealed class SignalRUserNotificationSink(
    IHubContext<UserNotificationsHub> hubContext,
    IOptions<ApplicationIdentityOptions> applicationIdentity,
    IOptions<NotificationSignalROptions> options) : IUserNotificationSink
{
    public string ProviderName => "signalr";
    public IReadOnlyCollection<string> DeliveryTags { get; } = [NotificationTags.Web];
    public NotificationSinkDeliveryMode DeliveryModes => NotificationSinkDeliveryMode.BestEffort;

    public async ValueTask<NotificationSinkDeliveryResult> DeliverAsync(
        NotificationSinkDeliveryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        UserNotificationMessage message = request.Message;

        if (!options.Value.Enabled)
        {
            return NotificationSinkDeliveryResult.Skipped("disabled");
        }

        string groupName = NotificationSignalRGroupNames.ForUser(
            applicationIdentity.Value.EffectiveNamespace,
            message.ScopeId,
            message.UserId);
        await hubContext.Clients
            .Group(groupName)
            .SendAsync(options.Value.ClientMethodName, message, cancellationToken)
            .ConfigureAwait(false);

        return NotificationSinkDeliveryResult.Delivered();
    }
}
