namespace Gma.Framework.Notifications.SignalR;

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Gma.Framework.Naming;
using Gma.Framework.Notifications;
using Gma.Framework.Runtime;
using Gma.Framework.Tenancy;

public sealed class UserNotificationsHub(
    IOptions<NotificationsOptions> notificationsOptions,
    IOptions<ApplicationIdentityOptions> applicationIdentity,
    IOptions<TenantOptions> tenantOptions) : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (!notificationsOptions.Value.Enabled)
        {
            this.RejectConnection("Notifications are disabled.");
        }

        string? userId = this.Context.User?.GetNotificationUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            this.RejectConnection("Notification user claim is required.");
        }

        string? tenantId = this.ResolveTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            this.RejectConnection("Notification tenant claim is required.");
        }

        string groupName = NotificationSignalRGroupNames.ForUser(
            applicationIdentity.Value.EffectiveNamespace,
            tenantId,
            userId);
        await this.Groups.AddToGroupAsync(this.Context.ConnectionId, groupName, this.Context.ConnectionAborted)
            .ConfigureAwait(false);
        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    private string? ResolveTenantId()
    {
        if (!tenantOptions.Value.Enabled)
        {
            return tenantOptions.Value.LocalDefaultTenantId;
        }

        return TenantIds.TryNormalize(this.Context.User?.GetTenantId(), out string? tenantId)
            ? tenantId
            : null;
    }

    [DoesNotReturn]
    private void RejectConnection(string message)
    {
        this.Context.Abort();
        throw new HubException(message);
    }
}
