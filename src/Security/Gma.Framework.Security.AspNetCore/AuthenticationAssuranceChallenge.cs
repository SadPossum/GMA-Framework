namespace Gma.Framework.Security.AspNetCore;

using System.Globalization;

public static class AuthenticationAssuranceChallenge
{
    public static string Create(AuthenticationAssuranceRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(requirement);

        List<string> parameters =
        [
            "error=\"insufficient_user_authentication\"",
            "error_description=\"A stronger or more recent authentication event is required.\""
        ];

        if (requirement.AcceptedContextReferences.Count > 0)
        {
            string values = string.Join(' ', requirement.AcceptedContextReferences);
            parameters.Add($"acr_values=\"{EscapeQuotedString(values)}\"");
        }

        if (requirement.MaxAuthenticationAge is not null)
        {
            long seconds = checked((long)Math.Ceiling(requirement.MaxAuthenticationAge.Value.TotalSeconds));
            parameters.Add($"max_age=\"{seconds.ToString(CultureInfo.InvariantCulture)}\"");
        }

        return $"Bearer {string.Join(", ", parameters)}";
    }

    private static string EscapeQuotedString(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}
