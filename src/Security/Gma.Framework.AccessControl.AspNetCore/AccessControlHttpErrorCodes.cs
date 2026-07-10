namespace Gma.Framework.AccessControl.AspNetCore;

public static class AccessControlHttpErrorCodes
{
    public const string Unauthenticated = "AccessControl.Unauthenticated";
    public const string Unauthorized = "AccessControl.Unauthorized";
    public const string ScopeResolverMissing = "AccessControl.ScopeResolverMissing";
    public const string ScopeRequired = "AccessControl.ScopeRequired";
    public const string ScopeInvalid = "AccessControl.ScopeInvalid";
}
