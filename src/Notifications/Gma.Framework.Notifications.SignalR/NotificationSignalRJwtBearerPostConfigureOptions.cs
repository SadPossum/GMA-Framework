namespace Gma.Framework.Notifications.SignalR;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Gma.Framework.Naming;
using Gma.Framework.Scoping;

internal sealed class NotificationSignalRJwtBearerPostConfigureOptions(
    IOptions<NotificationSignalROptions> signalROptions) : IPostConfigureOptions<JwtBearerOptions>
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

            IScopeContext scopeContext = context.HttpContext.RequestServices.GetRequiredService<IScopeContext>();
            if (!scopeContext.IsEnabled)
            {
                return;
            }

            if (!ScopeIds.TryNormalize(context.Principal.GetScopeId(), out _))
            {
                context.Fail("Notification scope claim is required.");
            }
        };
    }
}
