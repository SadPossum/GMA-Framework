namespace Gma.Framework.AccessControl;

using Gma.Framework.Permissions;

public interface IAccessGrantScopeReader
{
    Task<IReadOnlyList<AccessGrantScope>> ListGrantedScopesAsync(
        AccessSubject subject,
        PermissionCode permission,
        CancellationToken cancellationToken);
}
