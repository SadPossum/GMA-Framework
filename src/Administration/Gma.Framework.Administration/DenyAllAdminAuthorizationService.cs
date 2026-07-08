namespace Gma.Framework.Administration;

public sealed class DenyAllAdminAuthorizationService : IAdminAuthorizationService
{
    public Task<AdminAuthorizationResult> AuthorizeAsync(
        AdminActor actor,
        AdminPermission permission,
        string? tenantId,
        CancellationToken cancellationToken) =>
        Task.FromResult(AdminAuthorizationResult.Denied(AdminErrors.Unauthorized.Message));
}
