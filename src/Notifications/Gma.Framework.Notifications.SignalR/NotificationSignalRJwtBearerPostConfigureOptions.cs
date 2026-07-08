namespace Gma.Framework.Notifications.SignalR;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Gma.Framework.Naming;
using Gma.Framework.Tenancy;

internal sealed class NotificationSignalRJwtBearerPostConfigureOptions(
    IOptions<NotificationSignalROptions> signalROptions,
    IOptions<TenantOptions> tenantOptions) : IPostConfigureOptions<JwtBearerOptions>
{
    public void PostConfigure(string? name, JwtBearerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Func<MessageReceivedContext, Task> existingHandler = options.Events.OnMessageReceived;
        Func<TokenValidatedContext, Task> existingTokenValidatedHandler = options.Events.OnTokenValidated;
        options.Events.OnMessageReceived = async context =>
        {
            await existingHandler(context).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(context.Token))
            {
                return;
            }

            NotificationSignalROptions value = signalROptions.Value;
            if (!value.Enabled)
            {
                return;
            }

            if (!context.HttpContext.Request.Path.StartsWithSegments(value.HubPath))
            {
                return;
            }

            if (!context.Request.Query.TryGetValue(value.AccessTokenQueryParameter, out StringValues tokenValues) ||
                tokenValues.Count != 1 ||
                string.IsNullOrWhiteSpace(tokenValues[0]))
            {
                return;
            }

            context.Token = tokenValues[0];
        };
        options.Events.OnTokenValidated = async context =>
        {
            await existingTokenValidatedHandler(context).ConfigureAwait(false);

            NotificationSignalROptions value = signalROptions.Value;
            if (!value.Enabled ||
                context.Principal is null ||
                !context.HttpContext.Request.Path.StartsWithSegments(value.HubPath))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(context.Principal.GetNotificationUserId()))
            {
                context.Fail("Notification user claim is required.");
                return;
            }

            if (!tenantOptions.Value.Enabled)
            {
                return;
            }

            if (!TenantIds.TryNormalize(context.Principal.GetTenantId(), out _))
            {
                context.Fail("Notification tenant claim is required.");
            }
        };
    }
}
