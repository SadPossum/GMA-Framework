namespace Gma.Framework.AccessControl;

public sealed record AccessScopeMatchOptions(
    bool AllowAncestorScopeGrants = false,
    bool AllowGlobalScopeGrant = false);
