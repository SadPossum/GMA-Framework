namespace Gma.Framework.Notifications.SignalR;

using System.Security.Claims;
using Gma.Framework.Security;

internal static class NotificationClaimsPrincipalExtensions
{
    public static string? GetNotificationUserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier) ??
        user.FindFirstValue(ApplicationClaimNames.Subject);

    public static string? GetTenantId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ApplicationClaimNames.TenantId);
}
