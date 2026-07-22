namespace Gma.Framework.Api.Production;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

internal sealed class SanitizedUnhandledExceptionHandler(IProblemDetailsService problemDetailsService)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        ProblemDetails problemDetails = new()
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Http.UnexpectedFailure",
            Detail = "The request could not be completed. Use the trace identifier when contacting support."
        };
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;
        ProblemDetailsContext context = new()
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails
        };

        if (await problemDetailsService.TryWriteAsync(context).ConfigureAwait(false))
        {
            return true;
        }

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
