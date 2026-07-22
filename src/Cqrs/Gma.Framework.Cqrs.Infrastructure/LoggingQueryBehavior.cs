namespace Gma.Framework.Cqrs.Infrastructure;

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Observability.Infrastructure;

internal sealed class LoggingQueryBehavior<TQuery, TResponse>(
    ILogger<LoggingQueryBehavior<TQuery, TResponse>> logger,
    QueryMetrics metrics,
    IEnumerable<ICqrsLogScopeContributor>? scopeContributors = null)
    : IQueryPipelineBehavior<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(
        TQuery query,
        QueryNext<TResponse> next,
        CancellationToken cancellationToken)
    {
        string queryName = typeof(TQuery).Name;
        string moduleName = ModuleNameResolver.FromType(typeof(TQuery));
        long startedAt = Stopwatch.GetTimestamp();
        CqrsLogScopeContext scopeContext = new(
            moduleName,
            queryName,
            typeof(TQuery),
            CqrsRequestKind.Query);
        Dictionary<string, object?> scopeProperties = CqrsLogScopeBuilder.Create(scopeContext, scopeContributors);

        IDisposable? scope = this.BeginLogScope(scopeProperties);

        try
        {
            this.LogHandling(queryName);
            Result<TResponse> result = await next().ConfigureAwait(false);
            TimeSpan elapsed = Stopwatch.GetElapsedTime(startedAt);
            string? errorCode = result.IsFailure ? result.Error.Code : null;

            this.TryRecordMetrics(moduleName, queryName, result.IsSuccess, errorCode, elapsed);
            this.LogHandled(queryName, result.IsSuccess, elapsed);

            return result;
        }
        catch (Exception exception)
        {
            TimeSpan elapsed = Stopwatch.GetElapsedTime(startedAt);
            this.TryRecordMetrics(moduleName, queryName, isSuccess: false, exception.GetType().Name, elapsed);
            this.LogException(queryName, elapsed, exception);

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
            // Logging scopes are observability only; disposal failures must not affect query dispatch.
        }
    }

    private void TryRecordMetrics(
        string moduleName,
        string queryName,
        bool isSuccess,
        string? errorCode,
        TimeSpan elapsed)
    {
        try
        {
            metrics.Record(moduleName, queryName, isSuccess, errorCode, elapsed);
        }
        catch (Exception)
        {
            // Metrics are observability only; listener/exporter failures must not affect query dispatch.
        }
    }

    private void LogHandling(string queryName)
    {
        try
        {
            logger.LogDebug("Handling query {QueryName}", queryName);
        }
        catch (Exception)
        {
        }
    }

    private void LogHandled(string queryName, bool isSuccess, TimeSpan elapsed)
    {
        try
        {
            logger.LogInformation(
                "Handled query {QueryName} with result {Result} in {ElapsedMilliseconds} ms",
                queryName,
                isSuccess ? "success" : "failure",
                elapsed.TotalMilliseconds);
        }
        catch (Exception)
        {
        }
    }

    private void LogException(string queryName, TimeSpan elapsed, Exception exception)
    {
        try
        {
            logger.LogError(
                "Query {QueryName} failed with {ExceptionType} after {ElapsedMilliseconds} ms",
                queryName,
                exception.GetType().Name,
                elapsed.TotalMilliseconds);
        }
        catch (Exception)
        {
        }
    }
}
