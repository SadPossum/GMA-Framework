namespace Gma.Framework.Notifications.SignalR;

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Gma.Framework.Naming;
using Gma.Framework.Notifications;
using Gma.Framework.Runtime;
using Gma.Framework.Scoping;

public sealed class UserNotificationsHub(
    IOptions<NotificationsOptions> notificationsOptions,
    IOptions<ApplicationIdentityOptions> applicationIdentity,
    IOptions<ScopeOptions> scopeOptions) : Hub
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

        string? scopeId = this.ResolveScopeId();
        if (string.IsNullOrWhiteSpace(scopeId))
        {
            this.RejectConnection("Notification scope claim is required.");
        }

        string groupName = NotificationSignalRGroupNames.ForUser(
            applicationIdentity.Value.EffectiveNamespace,
            scopeId,
            userId);
        await this.Groups.AddToGroupAsync(this.Context.ConnectionId, groupName, this.Context.ConnectionAborted)
            .ConfigureAwait(false);
        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    private string? ResolveScopeId()
    {
        if (!scopeOptions.Value.Enabled)
        {
            return scopeOptions.Value.LocalDefaultScopeId;
        }

        return ScopeIds.TryNormalize(this.Context.User?.GetScopeId(), out string? scopeId)
            ? scopeId
            : null;
    }

    [DoesNotReturn]
    private void RejectConnection(string message)
    {
        this.Context.Abort();
        throw new HubException(message);
    }
}
