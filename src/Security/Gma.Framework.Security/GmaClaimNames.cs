namespace Gma.Framework.Security;

public static class GmaClaimNames
{
    public const int MaxLength = ApplicationClaimNames.MaxLength;

    public const string Subject = ApplicationClaimNames.Subject;
    public const string ScopeId = ApplicationClaimNames.ScopeId;
    public const string TenantId = ApplicationClaimNames.TenantId;
    public const string SessionId = ApplicationClaimNames.SessionId;

    public static bool IsValidClaimName(string? claimName) =>
        ApplicationClaimNames.IsValidClaimName(claimName);
}
