namespace Gma.Framework.Api.Production;

using Gma.Framework.Cqrs;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

internal sealed class OptimisticConcurrencyExceptionHandler(IProblemDetailsService problemDetailsService)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not OptimisticConcurrencyException concurrencyException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        ProblemDetails problemDetails = new()
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Persistence.ConcurrencyConflict",
            Detail = "The resource changed while this request was being processed. Reload it and retry."
        };
        problemDetails.Extensions["module"] = concurrencyException.ModuleName;
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails
        }).ConfigureAwait(false);
    }
}
