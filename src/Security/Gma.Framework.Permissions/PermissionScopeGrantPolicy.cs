namespace Gma.Framework.Permissions;

public sealed record PermissionScopeGrantPolicy(
    bool AllowAncestorScopeGrants = false,
    bool AllowGlobalScopeGrant = false)
{
    public static PermissionScopeGrantPolicy Exact { get; } = new();
    public static PermissionScopeGrantPolicy Descendants { get; } = new(AllowAncestorScopeGrants: true);
    public static PermissionScopeGrantPolicy GlobalAndDescendants { get; } = new(
        AllowAncestorScopeGrants: true,
        AllowGlobalScopeGrant: true);
}
