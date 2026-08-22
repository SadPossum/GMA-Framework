namespace Gma.Framework.Security.AspNetCore;

internal static class AuthenticationAssuranceRequirementComposer
{
    public static AuthenticationAssuranceRequirement Compose(
        IReadOnlyList<AuthenticationAssuranceRequirement> requirements)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        if (requirements.Count == 0)
        {
            throw new ArgumentException(
                "At least one authentication assurance requirement is required.",
                nameof(requirements));
        }

        List<string>? acceptedContextReferences = null;
        TimeSpan? maxAuthenticationAge = null;

        for (int index = requirements.Count - 1; index >= 0; index--)
        {
            AuthenticationAssuranceRequirement requirement = requirements[index] ??
                throw new ArgumentException(
                    "Authentication assurance requirements cannot contain null values.",
                    nameof(requirements));

            if (requirement.AcceptedContextReferences.Count > 0)
            {
                if (acceptedContextReferences is null)
                {
                    acceptedContextReferences = [.. requirement.AcceptedContextReferences];
                }
                else
                {
                    acceptedContextReferences.RemoveAll(candidate =>
                        !requirement.AcceptedContextReferences.Contains(candidate, StringComparer.Ordinal));
                }
            }

            if (requirement.MaxAuthenticationAge is not null &&
                (maxAuthenticationAge is null || requirement.MaxAuthenticationAge < maxAuthenticationAge))
            {
                maxAuthenticationAge = requirement.MaxAuthenticationAge;
            }
        }

        if (acceptedContextReferences is { Count: 0 })
        {
            throw new InvalidOperationException(
                "Layered authentication assurance requirements have no accepted authentication context in common. " +
                "Every context-constraining declaration must share at least one exact context reference.");
        }

        return new AuthenticationAssuranceRequirement(
            acceptedContextReferences,
            maxAuthenticationAge);
    }
}
