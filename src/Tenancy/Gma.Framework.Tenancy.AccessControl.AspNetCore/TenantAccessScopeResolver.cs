namespace Gma.Framework.Tenancy.AccessControl.AspNetCore;

using Gma.Framework.AccessControl;
using Gma.Framework.AccessControl.AspNetCore;
using Microsoft.AspNetCore.Http;

internal sealed class TenantAccessScopeResolver(ITenantContext tenantContext) : IAccessHttpScopeResolver
{
    public const string ResolverName = "tenant";
    public const string SegmentName = "tenant";

    public string Name => ResolverName;

    public ValueTask<AccessScopeResolutionResult> ResolveAsync(
        HttpContext httpContext,
        AccessPermissionMetadata metadata,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(metadata);

        if (string.IsNullOrWhiteSpace(tenantContext.TenantId))
        {
            return ValueTask.FromResult(AccessScopeResolutionResult.Failure(
                AccessControlHttpErrorCodes.ScopeRequired,
                "A tenant access scope is required.",
                StatusCodes.Status400BadRequest));
        }

        if (!AccessScopeSegment.TryCreate(SegmentName, tenantContext.TenantId, out AccessScopeSegment? segment))
        {
            return ValueTask.FromResult(AccessScopeResolutionResult.Failure(
                AccessControlHttpErrorCodes.ScopeInvalid,
                "The tenant access scope is invalid.",
                StatusCodes.Status400BadRequest));
        }

        return ValueTask.FromResult(AccessScopeResolutionResult.Success(AccessScope.Create(segment)));
    }
}
