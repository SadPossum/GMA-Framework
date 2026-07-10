namespace Gma.Framework.AccessControl.AspNetCore;

using Gma.Framework.AccessControl;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

internal sealed class AccessPermissionEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        Endpoint? endpoint = context.HttpContext.GetEndpoint();
        IReadOnlyList<AccessPermissionMetadata>? orderedMetadata =
            endpoint?.Metadata.GetOrderedMetadata<AccessPermissionMetadata>();
        AccessPermissionMetadata? metadata = orderedMetadata is { Count: > 0 }
            ? orderedMetadata[^1]
            : null;
        if (metadata is null)
        {
            return await next(context).ConfigureAwait(false);
        }

        IAccessHttpSubjectResolver subjectResolver =
            context.HttpContext.RequestServices.GetRequiredService<IAccessHttpSubjectResolver>();
        AccessSubject? subject = subjectResolver.ResolveSubject(context.HttpContext);
        if (subject is null)
        {
            return Problem(
                AccessControlHttpErrorCodes.Unauthenticated,
                "An authenticated subject is required.",
                StatusCodes.Status401Unauthorized);
        }

        AccessScopeResolutionResult scopeResult = await ResolveScopeAsync(context.HttpContext, metadata)
            .ConfigureAwait(false);
        if (!scopeResult.IsSuccess)
        {
            return Problem(scopeResult.ErrorCode!, scopeResult.ErrorMessage!, scopeResult.StatusCode);
        }

        IAccessAuthorizationService authorization =
            context.HttpContext.RequestServices.GetRequiredService<IAccessAuthorizationService>();
        AccessDecision decision = await authorization
            .AuthorizeAsync(new AccessRequirement(subject, metadata.Permission, scopeResult.Scope!), context.HttpContext.RequestAborted)
            .ConfigureAwait(false);

        if (!decision.IsAllowed)
        {
            return Problem(
                AccessControlHttpErrorCodes.Unauthorized,
                decision.Message ?? "The authenticated subject is not allowed to perform this action.",
                StatusCodes.Status403Forbidden);
        }

        return await next(context).ConfigureAwait(false);
    }

    private static async ValueTask<AccessScopeResolutionResult> ResolveScopeAsync(
        HttpContext httpContext,
        AccessPermissionMetadata metadata)
    {
        IEnumerable<IAccessHttpScopeResolver> resolvers =
            httpContext.RequestServices.GetServices<IAccessHttpScopeResolver>();
        string resolverName = metadata.ScopeResolverName ?? DefaultAccessHttpScopeResolver.ResolverName;
        IAccessHttpScopeResolver? resolver = resolvers
            .FirstOrDefault(candidate => string.Equals(candidate.Name, resolverName, StringComparison.Ordinal));

        return resolver is null
            ? AccessScopeResolutionResult.Failure(
                AccessControlHttpErrorCodes.ScopeResolverMissing,
                $"Access scope resolver '{resolverName}' is not registered.",
                StatusCodes.Status500InternalServerError)
            : await resolver.ResolveAsync(httpContext, metadata, httpContext.RequestAborted).ConfigureAwait(false);
    }

    private static IResult Problem(string title, string detail, int statusCode) =>
        Results.Problem(title: title, detail: detail, statusCode: statusCode);
}
