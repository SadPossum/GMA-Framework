namespace Gma.Framework.Administration.AccessControl;

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
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(permission);

        if (!PermissionCode.TryCreate(permission.Code, out PermissionCode? accessPermission))
        {
            return AdminAuthorizationResult.Denied(AdminErrors.Unauthorized.Message);
        }

        AccessScope scope = string.IsNullOrWhiteSpace(tenantId)
            ? AccessScope.Global
            : AccessScope.Create(AccessScopeSegment.Create("tenant", tenantId));

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
}
