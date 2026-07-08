namespace Gma.Framework.Notifications.Api;

using System.Net.ServerSentEvents;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Gma.Framework.Api.Tenancy;
using Gma.Framework.Naming;
using Gma.Framework.Notifications;
using Gma.Framework.Tenancy;

public static class UserNotificationSseEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapUserNotificationServerSentEvents(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        NotificationsOptions notificationsOptions = endpoints.ServiceProvider
            .GetRequiredService<IOptions<NotificationsOptions>>()
            .Value;
        NotificationSseOptions sseOptions = endpoints.ServiceProvider
            .GetRequiredService<IOptions<NotificationSseOptions>>()
            .Value;

        if (!notificationsOptions.Enabled || !sseOptions.Enabled)
        {
            return endpoints;
        }

        endpoints.MapGet(sseOptions.StreamPath, StreamAsync)
            .RequireAuthorization()
            .RequireTenant()
            .WithTags("Notifications");

        return endpoints;
    }

    private static IResult StreamAsync(HttpContext httpContext)
    {
        IUserNotificationFeed? feed = httpContext.RequestServices.GetService<IUserNotificationFeed>();
        if (feed is null)
        {
            return Results.Problem(
                title: "Notifications.NotConfigured",
                detail: "User notification streaming is enabled, but the notification runtime is not registered.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        ITenantContext tenantContext = httpContext.RequestServices.GetRequiredService<ITenantContext>();
        TenantOptions tenantOptions = httpContext.RequestServices.GetRequiredService<IOptions<TenantOptions>>().Value;
        NotificationSseOptions options = httpContext.RequestServices.GetRequiredService<IOptions<NotificationSseOptions>>().Value;
        string? userId = httpContext.User.GetNotificationUserId();

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.Unauthorized();
        }

        if (!TryResolveTenantId(httpContext, tenantContext, tenantOptions, out string? tenantId))
        {
            return Results.Forbid();
        }

        UserNotificationTarget target = UserNotificationTarget.User(tenantId, userId);
        return TypedResults.ServerSentEvents(
            ReadStreamAsync(feed, target, options, httpContext.RequestAborted));
    }

    private static async IAsyncEnumerable<SseItem<NotificationSseItem>> ReadStreamAsync(
        IUserNotificationFeed feed,
        UserNotificationTarget target,
        NotificationSseOptions options,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using IUserNotificationSubscription subscription = feed.Subscribe(target, cancellationToken);
        await using IAsyncEnumerator<UserNotificationMessage> notifications =
            subscription.ReadAllAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        using PeriodicTimer heartbeat = new(options.HeartbeatInterval);

        Task<bool> notificationTask = notifications.MoveNextAsync().AsTask();
        Task<bool> heartbeatTask = heartbeat.WaitForNextTickAsync(cancellationToken).AsTask();

        while (!cancellationToken.IsCancellationRequested)
        {
            Task completed = await Task.WhenAny(notificationTask, heartbeatTask).ConfigureAwait(false);

            if (completed == notificationTask)
            {
                if (!await notificationTask.ConfigureAwait(false))
                {
                    yield break;
                }

                yield return new SseItem<NotificationSseItem>(
                    NotificationSseItem.FromNotification(notifications.Current),
                    options.NotificationEventType);
                notificationTask = notifications.MoveNextAsync().AsTask();
                continue;
            }

            if (!await heartbeatTask.ConfigureAwait(false))
            {
                yield break;
            }

            yield return new SseItem<NotificationSseItem>(
                NotificationSseItem.Heartbeat(),
                "heartbeat");
            heartbeatTask = heartbeat.WaitForNextTickAsync(cancellationToken).AsTask();
        }
    }

    private static bool TryResolveTenantId(
        HttpContext httpContext,
        ITenantContext tenantContext,
        TenantOptions tenantOptions,
        out string tenantId)
    {
        if (!tenantContext.IsEnabled)
        {
            tenantId = tenantOptions.LocalDefaultTenantId;
            return !string.IsNullOrWhiteSpace(tenantId);
        }

        tenantId = tenantContext.TenantId ?? string.Empty;
        string? tokenTenantId = httpContext.User.GetTenantId();
        if (!TenantIds.TryNormalize(tokenTenantId, out string? normalizedTokenTenantId) ||
            !string.Equals(normalizedTokenTenantId, tenantContext.TenantId, StringComparison.Ordinal))
        {
            return false;
        }

        tenantId = normalizedTokenTenantId;
        return true;
    }
}
