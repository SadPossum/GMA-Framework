namespace Gma.Framework.Security;

public static class ApplicationClaimNames
{
    public const int MaxLength = 256;

    public const string Subject = "sub";
    public const string ScopeId = "scope_id";
    public const string TenantId = ScopeId;
    public const string SessionId = "sid";
    public const string AuthenticationContextReference = "acr";
    public const string AuthenticationMethodReference = "amr";
    public const string AuthenticationTime = "auth_time";

    public static bool IsValidClaimName(string? claimName) =>
        !string.IsNullOrWhiteSpace(claimName) &&
        claimName.Length <= MaxLength &&
        !claimName.Any(character => char.IsWhiteSpace(character) || char.IsControl(character));
}
