namespace Gma.Framework.Notifications.Cqrs;

using Microsoft.Extensions.Logging;
using Gma.Framework.Cqrs;
using Gma.Framework.Notifications.Infrastructure;
using Gma.Framework.Observability.Infrastructure;
using Gma.Framework.Results;

internal sealed class NotificationRequestCommandBehavior<TCommand, TResponse>(
    IUserNotificationRequestQueueFlusher requestQueue,
    ILogger<NotificationRequestCommandBehavior<TCommand, TResponse>>? logger = null)
    : ICommandPipelineBehavior<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(
        TCommand command,
        CommandNext<TResponse> next,
        CancellationToken cancellationToken)
    {
        Result<TResponse> result = await next().ConfigureAwait(false);

        if (result.IsSuccess)
        {
            try
            {
                await requestQueue.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                this.LogFlushFailure(exception);
            }
        }

        return result;
    }

    private void LogFlushFailure(Exception exception)
    {
        if (logger is null)
        {
            return;
        }

        try
        {
            logger.LogWarning(
                "User notification request flush failed open after successful command {CommandName} in module {Module} with {ExceptionType}",
                typeof(TCommand).Name,
                ModuleNameResolver.FromType(typeof(TCommand)),
                exception.GetType().Name);
        }
        catch (Exception)
        {
            // Notification delivery is best-effort after commit; observability must not fail a committed command.
        }
    }
}
