namespace Gma.Framework.AccessControl;

public static class AccessDecisionReasonCodes
{
    public const string Allowed = "access.allowed";
    public const string DenyByDefault = "access.deny-by-default";
    public const string ProviderDenied = "access.provider-denied";
    public const string ProviderAbstained = "access.provider-abstained";
}
