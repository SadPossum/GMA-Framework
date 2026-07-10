namespace Gma.Framework.AccessControl;

using Gma.Framework.Permissions;

public interface IAccessScopeMatchOptionsResolver
{
    AccessScopeMatchOptions Resolve(PermissionCode permission);
}
