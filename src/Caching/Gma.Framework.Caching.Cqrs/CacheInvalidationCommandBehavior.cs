namespace Gma.Framework.Caching.Cqrs;

using Microsoft.Extensions.Logging;
using Gma.Framework.Caching.Infrastructure;
using Gma.Framework.Cqrs;
using Gma.Framework.Observability.Infrastructure;
using Gma.Framework.Results;

internal sealed class CacheInvalidationCommandBehavior<TCommand, TResponse>(
    ICacheInvalidationQueueFlusher invalidationQueue,
    ILogger<CacheInvalidationCommandBehavior<TCommand, TResponse>>? logger = null)
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
                await invalidationQueue.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                this.LogInvalidationFailure(exception);
            }
        }

        return result;
    }

    private void LogInvalidationFailure(Exception exception)
    {
        if (logger is null)
        {
            return;
        }

        try
        {
            string commandName = typeof(TCommand).Name;
            string moduleName = ModuleNameResolver.FromType(typeof(TCommand));
            logger.LogWarning(
                "Cache invalidation failed open after successful command {CommandName} in module {Module} with {ExceptionType}",
                commandName,
                moduleName,
                exception.GetType().Name);
        }
        catch (Exception)
        {
            // Cache invalidation is an optimization; observability failures must not fail a committed command.
        }
    }
}
