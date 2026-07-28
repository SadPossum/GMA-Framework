namespace Gma.Framework.Administration.AccessControl;

using System.Diagnostics.CodeAnalysis;
using Gma.Framework.AccessControl;
using Gma.Framework.Administration;
using Gma.Framework.Permissions;

internal sealed class AccessControlAdminAuthorizationService(IAccessAuthorizationService authorization)
    : IAdminAuthorizationService
{
    public async Task<AdminAuthorizationResult> AuthorizeAsync(
        AdminActor actor,
        AdminPermission permission,
        string? tenantId,
        CancellationToken cancellationToken) =>
        await this.AuthorizeAsync(
            actor,
            permission,
            tenantId,
            resourceScope: null,
            cancellationToken).ConfigureAwait(false);

    public async Task<AdminAuthorizationResult> AuthorizeAsync(
        AdminActor actor,
        AdminPermission permission,
        string? tenantId,
        AdminResourceScope? resourceScope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(permission);

        if (!PermissionCode.TryCreate(permission.Code, out PermissionCode? accessPermission))
        {
            return AdminAuthorizationResult.Denied(AdminErrors.Unauthorized.Message);
        }

        if (!TryCreateAccessScope(tenantId, resourceScope, out AccessScope? scope))
        {
            return AdminAuthorizationResult.Denied(AdminErrors.Unauthorized.Message);
        }

        AccessDecision decision = await authorization
            .AuthorizeAsync(
                new AccessRequirement(
                    AccessSubject.AdminActor(actor.Id),
                    accessPermission,
                    scope),
                cancellationToken)
            .ConfigureAwait(false);

        return decision.IsAllowed
            ? AdminAuthorizationResult.Allowed()
            : AdminAuthorizationResult.Denied(decision.Message ?? AdminErrors.Unauthorized.Message);
    }

    private static bool TryCreateAccessScope(
        string? tenantId,
        AdminResourceScope? resourceScope,
        [NotNullWhen(true)] out AccessScope? scope)
    {
        scope = null;
        List<AccessScopeSegment> segments = [];
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            if (!AccessScopeSegment.TryCreate("tenant", tenantId, out AccessScopeSegment? tenantSegment))
            {
                return false;
            }

            segments.Add(tenantSegment);
        }

        if (resourceScope is not null)
        {
            foreach (AdminResourceScopeSegment segment in resourceScope.Segments)
            {
                if (!AccessScopeSegment.TryCreate(
                        segment.Name,
                        segment.Value,
                        out AccessScopeSegment? accessSegment))
                {
                    return false;
                }

                segments.Add(accessSegment);
            }
        }

        try
        {
            scope = AccessScope.Create(segments);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
