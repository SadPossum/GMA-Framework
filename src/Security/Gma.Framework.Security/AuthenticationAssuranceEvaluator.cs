namespace Gma.Framework.Security;

using System.Globalization;
using System.Security.Claims;

public static class AuthenticationAssuranceEvaluator
{
    public static bool IsSatisfied(
        ClaimsPrincipal principal,
        AuthenticationAssuranceRequirement requirement,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(requirement);

        if (principal.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (requirement.AcceptedContextReferences.Count > 0)
        {
            string? contextReference = principal.FindFirst(
                ApplicationClaimNames.AuthenticationContextReference)?.Value;
            if (contextReference is null ||
                !requirement.AcceptedContextReferences.Contains(
                    contextReference,
                    StringComparer.Ordinal))
            {
                return false;
            }
        }

        if (requirement.MaxAuthenticationAge is null)
        {
            return true;
        }

        string? value = principal.FindFirst(ApplicationClaimNames.AuthenticationTime)?.Value;
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long unixTimeSeconds))
        {
            return false;
        }

        DateTimeOffset authenticatedAtUtc;
        try
        {
            authenticatedAtUtc = DateTimeOffset.FromUnixTimeSeconds(unixTimeSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        DateTimeOffset normalizedNowUtc = nowUtc.ToUniversalTime();
        return authenticatedAtUtc <= normalizedNowUtc &&
            normalizedNowUtc - authenticatedAtUtc <= requirement.MaxAuthenticationAge.Value;
    }
}
