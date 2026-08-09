namespace Gma.Framework.Api.Production;

using Gma.Framework.Cqrs;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

internal sealed class TransactionCoordinationExceptionHandler(IProblemDetailsService problemDetailsService)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not TransactionCoordinationException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        httpContext.Response.Headers.RetryAfter = "1";
        ProblemDetails problemDetails = new()
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = "Persistence.CoordinationUnavailable",
            Detail = "The operation could not obtain temporary coordination. Retry the complete request."
        };
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails
        }).ConfigureAwait(false);
    }
}
