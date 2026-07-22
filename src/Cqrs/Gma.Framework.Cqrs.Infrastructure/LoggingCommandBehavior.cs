namespace Gma.Framework.Cqrs.Infrastructure;

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Observability.Infrastructure;

internal sealed class LoggingCommandBehavior<TCommand, TResponse>(
    ILogger<LoggingCommandBehavior<TCommand, TResponse>> logger,
    CommandMetrics metrics,
    IEnumerable<ICqrsLogScopeContributor>? scopeContributors = null)
    : ICommandPipelineBehavior<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(
        TCommand command,
        CommandNext<TResponse> next,
        CancellationToken cancellationToken)
    {
        string commandName = typeof(TCommand).Name;
        string moduleName = ModuleNameResolver.FromType(typeof(TCommand));
        long startedAt = Stopwatch.GetTimestamp();
        CqrsLogScopeContext scopeContext = new(
            moduleName,
            commandName,
            typeof(TCommand),
            CqrsRequestKind.Command);
        Dictionary<string, object?> scopeProperties = CqrsLogScopeBuilder.Create(scopeContext, scopeContributors);

        IDisposable? scope = this.BeginLogScope(scopeProperties);

        try
        {
            this.LogHandling(commandName);
            Result<TResponse> result = await next().ConfigureAwait(false);
            TimeSpan elapsed = Stopwatch.GetElapsedTime(startedAt);
            string? errorCode = result.IsFailure ? result.Error.Code : null;

            this.TryRecordMetrics(moduleName, commandName, result.IsSuccess, errorCode, elapsed);
            this.LogHandled(commandName, result.IsSuccess, elapsed);

            return result;
        }
        catch (Exception exception)
        {
            TimeSpan elapsed = Stopwatch.GetElapsedTime(startedAt);
            this.TryRecordMetrics(moduleName, commandName, isSuccess: false, exception.GetType().Name, elapsed);
            this.LogException(commandName, elapsed, exception);

            throw;
        }
        finally
        {
            DisposeLogScope(scope);
        }
    }

    private IDisposable? BeginLogScope(Dictionary<string, object?> scopeProperties)
    {
        try
        {
            return logger.BeginScope(scopeProperties);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void DisposeLogScope(IDisposable? scope)
    {
        try
        {
            scope?.Dispose();
        }
        catch (Exception)
        {
            // Logging scopes are observability only; disposal failures must not affect command dispatch.
        }
    }

    private void TryRecordMetrics(
        string moduleName,
        string commandName,
        bool isSuccess,
        string? errorCode,
        TimeSpan elapsed)
    {
        try
        {
            metrics.Record(moduleName, commandName, isSuccess, errorCode, elapsed);
        }
        catch (Exception)
        {
            // Metrics are observability only; listener/exporter failures must not affect command dispatch.
        }
    }

    private void LogHandling(string commandName)
    {
        try
        {
            logger.LogDebug("Handling command {CommandName}", commandName);
        }
        catch (Exception)
        {
        }
    }

    private void LogHandled(string commandName, bool isSuccess, TimeSpan elapsed)
    {
        try
        {
            logger.LogInformation(
                "Handled command {CommandName} with result {Result} in {ElapsedMilliseconds} ms",
                commandName,
                isSuccess ? "success" : "failure",
                elapsed.TotalMilliseconds);
        }
        catch (Exception)
        {
        }
    }

    private void LogException(string commandName, TimeSpan elapsed, Exception exception)
    {
        try
        {
            logger.LogError(
                "Command {CommandName} failed with {ExceptionType} after {ElapsedMilliseconds} ms",
                commandName,
                exception.GetType().Name,
                elapsed.TotalMilliseconds);
        }
        catch (Exception)
        {
        }
    }
}
