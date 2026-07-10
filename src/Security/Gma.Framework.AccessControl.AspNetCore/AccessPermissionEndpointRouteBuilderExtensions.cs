namespace Gma.Framework.AccessControl.AspNetCore;

using Gma.Framework.AccessControl;
using Gma.Framework.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

public static class AccessPermissionEndpointRouteBuilderExtensions
{
    public static RouteHandlerBuilder RequirePermission(
        this RouteHandlerBuilder builder,
        string permissionCode) =>
        RequirePermission(builder, PermissionCode.Create(permissionCode));

    public static RouteHandlerBuilder RequirePermission(
        this RouteHandlerBuilder builder,
        PermissionCode permission,
        AccessScope? scope = null,
        bool requireScope = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        AddMetadata(builder, new AccessPermissionMetadata(permission, scope, requireScope: requireScope));
        return builder.AddEndpointFilter<AccessPermissionEndpointFilter>();
    }

    public static RouteHandlerBuilder RequireRouteScopePermission(
        this RouteHandlerBuilder builder,
        string permissionCode,
        string scopeSegmentName,
        string routeValueName) =>
        RequireRouteScopePermission(builder, PermissionCode.Create(permissionCode), scopeSegmentName, routeValueName);

    public static RouteHandlerBuilder RequireRouteScopePermission(
        this RouteHandlerBuilder builder,
        PermissionCode permission,
        string scopeSegmentName,
        string routeValueName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        AddMetadata(builder, new AccessPermissionMetadata(
            permission,
            scopeSegmentName: scopeSegmentName,
            scopeRouteValueName: routeValueName,
            requireScope: true));
        return builder.AddEndpointFilter<AccessPermissionEndpointFilter>();
    }

    public static RouteHandlerBuilder RequireResolvedScopePermission(
        this RouteHandlerBuilder builder,
        string permissionCode,
        string scopeResolverName) =>
        RequireResolvedScopePermission(builder, PermissionCode.Create(permissionCode), scopeResolverName);

    public static RouteHandlerBuilder RequireResolvedScopePermission(
        this RouteHandlerBuilder builder,
        PermissionCode permission,
        string scopeResolverName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        AddMetadata(builder, new AccessPermissionMetadata(
            permission,
            scopeResolverName: scopeResolverName,
            requireScope: true));
        return builder.AddEndpointFilter<AccessPermissionEndpointFilter>();
    }

    public static RouteGroupBuilder RequirePermission(
        this RouteGroupBuilder builder,
        string permissionCode) =>
        RequirePermission(builder, PermissionCode.Create(permissionCode));

    public static RouteGroupBuilder RequirePermission(
        this RouteGroupBuilder builder,
        PermissionCode permission,
        AccessScope? scope = null,
        bool requireScope = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        AddMetadata(builder, new AccessPermissionMetadata(permission, scope, requireScope: requireScope));
        return builder.AddEndpointFilter<AccessPermissionEndpointFilter>();
    }

    public static RouteGroupBuilder RequireResolvedScopePermission(
        this RouteGroupBuilder builder,
        string permissionCode,
        string scopeResolverName) =>
        RequireResolvedScopePermission(builder, PermissionCode.Create(permissionCode), scopeResolverName);

    public static RouteGroupBuilder RequireResolvedScopePermission(
        this RouteGroupBuilder builder,
        PermissionCode permission,
        string scopeResolverName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        AddMetadata(builder, new AccessPermissionMetadata(
            permission,
            scopeResolverName: scopeResolverName,
            requireScope: true));
        return builder.AddEndpointFilter<AccessPermissionEndpointFilter>();
    }

    private static void AddMetadata(IEndpointConventionBuilder builder, AccessPermissionMetadata metadata) =>
        builder.Add(endpointBuilder => endpointBuilder.Metadata.Add(metadata));
}
