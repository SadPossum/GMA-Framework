namespace Gma.Framework.Security;

public static class ApplicationClaimNames
{
    public const int MaxLength = 256;

    public const string Subject = "sub";
    public const string TenantId = "tenant_id";
    public const string SessionId = "sid";

    public static bool IsValidClaimName(string? claimName) =>
        !string.IsNullOrWhiteSpace(claimName) &&
        claimName.Length <= MaxLength &&
        !claimName.Any(character => char.IsWhiteSpace(character) || char.IsControl(character));
}
