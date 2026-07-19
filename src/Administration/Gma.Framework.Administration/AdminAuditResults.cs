namespace Gma.Framework.Administration;

public static class AdminAuditResults
{
    public const int MaxLength = 32;

    public const string Succeeded = "succeeded";
    public const string Denied = "denied";
    public const string Failed = "failed";

    public static AdminAuditResult Parse(string result)
    {
        if (TryParse(result, out AdminAuditResult parsed))
        {
            return parsed;
        }

        if (string.IsNullOrWhiteSpace(result))
        {
            throw new ArgumentException("Admin audit result is required.", nameof(result));
        }

        throw new ArgumentException("Admin audit result is not supported.", nameof(result));
    }

    public static bool TryParse(string? result, out AdminAuditResult parsed)
    {
        parsed = AdminAuditResult.Unknown;
        if (string.IsNullOrWhiteSpace(result))
        {
            return false;
        }

        parsed = result.Trim().ToLowerInvariant() switch
        {
            Succeeded => AdminAuditResult.Succeeded,
            Denied => AdminAuditResult.Denied,
            Failed => AdminAuditResult.Failed,
            _ => AdminAuditResult.Unknown
        };

        return parsed is not AdminAuditResult.Unknown;
    }

    public static string ToWireName(AdminAuditResult result) =>
        result switch
        {
            AdminAuditResult.Succeeded => Succeeded,
            AdminAuditResult.Denied => Denied,
            AdminAuditResult.Failed => Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, "Admin audit result is invalid.")
        };
}
