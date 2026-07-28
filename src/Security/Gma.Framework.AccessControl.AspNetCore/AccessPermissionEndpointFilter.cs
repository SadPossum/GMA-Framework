namespace Gma.Framework.AccessControl.AspNetCore;

using Gma.Framework.AccessControl;
using Gma.Framework.Observability;
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

        IResult? denied = await AuthorizeAsync(
            context.HttpContext,
            [metadata]).ConfigureAwait(false);
        return denied ?? await next(context).ConfigureAwait(false);
    }

    internal static async ValueTask<IResult?> AuthorizeAsync(
        HttpContext httpContext,
        IReadOnlyList<AccessPermissionMetadata> requirements)
    {
        IAccessHttpSubjectResolver subjectResolver =
            httpContext.RequestServices.GetRequiredService<IAccessHttpSubjectResolver>();
        AccessSubject? subject = subjectResolver.ResolveSubject(httpContext);
        if (subject is null)
        {
            RecordSignal(
                httpContext,
                AccessControlSecuritySignalDefinitions.SubjectMissing);
            return Problem(
                AccessControlHttpErrorCodes.Unauthenticated,
                "An authenticated subject is required.",
                StatusCodes.Status401Unauthorized);
        }

        IAccessAuthorizationService authorization =
            httpContext.RequestServices.GetRequiredService<IAccessAuthorizationService>();
        foreach (AccessPermissionMetadata requirement in requirements)
        {
            AccessScopeResolutionResult scopeResult = await ResolveScopeAsync(
                httpContext,
                requirement).ConfigureAwait(false);
            if (!scopeResult.IsSuccess)
            {
                RecordSignal(
                    httpContext,
                    scopeResult.StatusCode is >= 400 and < 500
                        ? AccessControlSecuritySignalDefinitions.ScopeDenied
                        : AccessControlSecuritySignalDefinitions.ScopeResolutionFailed);
                return Problem(
                    scopeResult.ErrorCode!,
                    scopeResult.ErrorMessage!,
                    scopeResult.StatusCode);
            }

            AccessDecision decision = await authorization
                .AuthorizeAsync(
                    new AccessRequirement(
                        subject,
                        requirement.Permission,
                        scopeResult.Scope!),
                    httpContext.RequestAborted)
                .ConfigureAwait(false);
            if (!decision.IsAllowed)
            {
                RecordSignal(
                    httpContext,
                    AccessControlSecuritySignalDefinitions.PermissionDenied);
                return Problem(
                    AccessControlHttpErrorCodes.Unauthorized,
                    decision.Message ??
                        "The authenticated subject is not allowed to perform this action.",
                    StatusCodes.Status403Forbidden);
            }
        }

        return null;
    }

    private static void RecordSignal(
        HttpContext httpContext,
        SecuritySignalDefinition definition) =>
        httpContext.RequestServices
            .GetRequiredService<ISecuritySignalRecorder>()
            .Record(definition);

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
