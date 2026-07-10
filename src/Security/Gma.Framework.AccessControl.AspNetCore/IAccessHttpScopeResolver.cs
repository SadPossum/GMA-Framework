namespace Gma.Framework.AccessControl.AspNetCore;

using Microsoft.AspNetCore.Http;

public interface IAccessHttpScopeResolver
{
    string Name { get; }

    ValueTask<AccessScopeResolutionResult> ResolveAsync(
        HttpContext httpContext,
        AccessPermissionMetadata metadata,
        CancellationToken cancellationToken);
}
