namespace Gma.Framework.Api.Serilog;

using System.Diagnostics;
using global::Serilog;
using global::Serilog.Events;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Gma.Framework.Api.Observability;
using Gma.Framework.Observability;

internal static class SafeRequestLoggingMiddleware
{
    private const string UnmatchedRoute = "unmatched";

    private const string RequestCompletedMessage =
        "HTTP {RequestMethod} {RouteTemplate} responded {StatusCode} in {ElapsedMilliseconds:0.0000} ms";

    public static async Task InvokeAsync(HttpContext httpContext, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(next);

        long startedAt = Stopwatch.GetTimestamp();
        Exception? requestException = null;

        try
        {
            await next(httpContext);
        }
        catch (Exception exception)
        {
            requestException = exception;
            throw;
        }
        finally
        {
            ILogger logger = httpContext.RequestServices.GetService<ILogger>() ?? Log.Logger;
            WriteCompletion(httpContext, logger, startedAt, requestException);
        }
    }

    private static void WriteCompletion(
        HttpContext httpContext,
        ILogger logger,
        long startedAt,
        Exception? requestException)
    {
        SafeDiagnosticContext diagnosticContext = new();
        string traceId = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
        ModuleEndpointMetadata? module = httpContext.GetEndpoint()?.Metadata.GetMetadata<ModuleEndpointMetadata>();

        diagnosticContext.SetFrameworkProperty(ObservabilityLogPropertyNames.TraceId, traceId);

        if (module is not null)
        {
            diagnosticContext.SetFrameworkProperty(ObservabilityLogPropertyNames.Module, module.ModuleName);
        }

        EnrichDiagnosticContext(diagnosticContext, httpContext);

        if (requestException is not null)
        {
            diagnosticContext.SetFrameworkProperty("ExceptionType", requestException.GetType().Name);
        }

        ILogger contextualLogger = logger;

        foreach ((string propertyName, object? propertyValue) in diagnosticContext.Properties)
        {
            contextualLogger = contextualLogger.ForContext(propertyName, propertyValue);
        }

        int statusCode = requestException is null
            ? httpContext.Response.StatusCode
            : StatusCodes.Status500InternalServerError;
        LogEventLevel level = GetLevel(statusCode, requestException);
        string routeTemplate = GetRouteTemplate(httpContext);
        double elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

        contextualLogger.Write(
            level,
            RequestCompletedMessage,
            httpContext.Request.Method,
            routeTemplate,
            statusCode,
            elapsedMilliseconds);
    }

    private static void EnrichDiagnosticContext(SafeDiagnosticContext diagnosticContext, HttpContext httpContext)
    {
        foreach (IRequestLoggingDiagnosticContextContributor contributor in httpContext.RequestServices
                     .GetServices<IRequestLoggingDiagnosticContextContributor>())
        {
            try
            {
                contributor.Enrich(diagnosticContext, httpContext);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Request logging stays best effort; enrichment cannot fail request execution.
            }
        }
    }

    private static string GetRouteTemplate(HttpContext httpContext) =>
        (httpContext.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? UnmatchedRoute;

    private static LogEventLevel GetLevel(int statusCode, Exception? requestException)
    {
        if (requestException is not null || statusCode >= StatusCodes.Status500InternalServerError)
        {
            return LogEventLevel.Error;
        }

        return statusCode >= StatusCodes.Status400BadRequest
            ? LogEventLevel.Warning
            : LogEventLevel.Information;
    }
}
