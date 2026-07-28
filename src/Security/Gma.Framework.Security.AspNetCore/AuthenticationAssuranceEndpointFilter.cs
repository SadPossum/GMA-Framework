namespace Gma.Framework.Security.AspNetCore;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

internal sealed class AuthenticationAssuranceEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        Endpoint? endpoint = context.HttpContext.GetEndpoint();
        IReadOnlyList<AuthenticationAssuranceMetadata>? orderedMetadata =
            endpoint?.Metadata.GetOrderedMetadata<AuthenticationAssuranceMetadata>();
        AuthenticationAssuranceRequirement? requirement = orderedMetadata is { Count: > 0 }
            ? orderedMetadata[^1].Requirement
            : null;
        if (requirement is null)
        {
            return await next(context).ConfigureAwait(false);
        }

        TimeProvider timeProvider =
            context.HttpContext.RequestServices.GetService<TimeProvider>() ?? TimeProvider.System;
        if (!AuthenticationAssuranceEvaluator.IsSatisfied(
                context.HttpContext.User,
                requirement,
                timeProvider.GetUtcNow()))
        {
            return Challenge(context.HttpContext, requirement);
        }

        return await next(context).ConfigureAwait(false);
    }

    private static IResult Challenge(
        HttpContext httpContext,
        AuthenticationAssuranceRequirement requirement)
    {
        httpContext.Response.Headers.WWWAuthenticate =
            AuthenticationAssuranceChallenge.Create(requirement);
        return Results.Problem(
            title: AuthenticationAssuranceHttpErrorCodes.InsufficientAuthentication,
            detail: "A stronger or more recent authentication event is required.",
            statusCode: StatusCodes.Status401Unauthorized);
    }
}
