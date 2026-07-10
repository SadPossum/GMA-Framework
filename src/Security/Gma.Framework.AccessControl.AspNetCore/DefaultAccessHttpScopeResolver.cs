namespace Gma.Framework.AccessControl.AspNetCore;

using Gma.Framework.AccessControl;
using Microsoft.AspNetCore.Http;

internal sealed class DefaultAccessHttpScopeResolver : IAccessHttpScopeResolver
{
    public const string ResolverName = "default";

    public string Name => ResolverName;

    public ValueTask<AccessScopeResolutionResult> ResolveAsync(
        HttpContext httpContext,
        AccessPermissionMetadata metadata,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(metadata);

        if (metadata.Scope is not null)
        {
            return ValueTask.FromResult(AccessScopeResolutionResult.Success(metadata.Scope));
        }

        if (metadata.ScopeRouteValueName is not null)
        {
            object? routeValue = httpContext.Request.RouteValues[metadata.ScopeRouteValueName];
            string? routeScopeValue = routeValue?.ToString();

            if (string.IsNullOrWhiteSpace(routeScopeValue))
            {
                return ValueTask.FromResult(AccessScopeResolutionResult.Failure(
                    AccessControlHttpErrorCodes.ScopeRequired,
                    "A route scope value is required.",
                    StatusCodes.Status400BadRequest));
            }

            if (!AccessScopeSegment.TryCreate(metadata.ScopeSegmentName, routeScopeValue, out AccessScopeSegment? segment))
            {
                return ValueTask.FromResult(AccessScopeResolutionResult.Failure(
                    AccessControlHttpErrorCodes.ScopeInvalid,
                    "The route scope value is invalid.",
                    StatusCodes.Status400BadRequest));
            }

            return ValueTask.FromResult(AccessScopeResolutionResult.Success(AccessScope.Create(segment)));
        }

        return ValueTask.FromResult(metadata.RequireScope
            ? AccessScopeResolutionResult.Failure(
                AccessControlHttpErrorCodes.ScopeRequired,
                "An access scope is required.",
                StatusCodes.Status400BadRequest)
            : AccessScopeResolutionResult.Success(AccessScope.Global));
    }
}
