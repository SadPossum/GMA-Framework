namespace Gma.Framework.Administration;

public interface IAdminAuthorizationService
{
    Task<AdminAuthorizationResult> AuthorizeAsync(
        AdminActor actor,
        AdminPermission permission,
        string? tenantId,
        CancellationToken cancellationToken);
}
