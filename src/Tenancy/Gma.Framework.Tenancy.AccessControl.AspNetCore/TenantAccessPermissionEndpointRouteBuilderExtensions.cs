namespace Gma.Framework.Tenancy.AccessControl.AspNetCore;

using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

public static class TenantAccessPermissionEndpointRouteBuilderExtensions
{
    public static RouteHandlerBuilder RequireTenantPermission(
        this RouteHandlerBuilder builder,
        string permissionCode) =>
        RequireTenantPermission(builder, PermissionCode.Create(permissionCode));

    public static RouteHandlerBuilder RequireTenantPermission(
        this RouteHandlerBuilder builder,
        PermissionCode permission)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.RequireResolvedScopePermission(permission, TenantAccessScopeResolver.ResolverName);
    }

    public static RouteGroupBuilder RequireTenantPermission(
        this RouteGroupBuilder builder,
        string permissionCode) =>
        RequireTenantPermission(builder, PermissionCode.Create(permissionCode));

    public static RouteGroupBuilder RequireTenantPermission(
        this RouteGroupBuilder builder,
        PermissionCode permission)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.RequireResolvedScopePermission(permission, TenantAccessScopeResolver.ResolverName);
    }
}
