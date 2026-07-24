namespace Gma.Framework.Tenancy.AccessControl.AspNetCore;

using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Permissions;

public static class TenantAccessPermissionMetadata
{
    public static AccessPermissionMetadata Create(string permissionCode) =>
        Create(PermissionCode.Create(permissionCode));

    public static AccessPermissionMetadata Create(PermissionCode permission) =>
        new(
            permission,
            scopeResolverName: TenantAccessScopeResolver.ResolverName,
            requireScope: true);
}
