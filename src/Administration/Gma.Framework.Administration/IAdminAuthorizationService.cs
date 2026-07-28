namespace Gma.Framework.Administration;

public interface IAdminAuthorizationService
{
    Task<AdminAuthorizationResult> AuthorizeAsync(
        AdminActor actor,
        AdminPermission permission,
        string? tenantId,
        CancellationToken cancellationToken);

    Task<AdminAuthorizationResult> AuthorizeAsync(
        AdminActor actor,
        AdminPermission permission,
        string? tenantId,
        AdminResourceScope? resourceScope,
        CancellationToken cancellationToken) =>
        this.AuthorizeAsync(actor, permission, tenantId, cancellationToken);
}
