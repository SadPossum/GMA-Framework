namespace Gma.Framework.Security.AspNetCore;

internal sealed record AuthenticationAssuranceMetadata(AuthenticationAssuranceRequirement Requirement);

internal sealed record EffectiveAuthenticationAssuranceMetadata(
    AuthenticationAssuranceRequirement Requirement);

internal sealed class AuthenticationAssuranceFilterRegistrationMetadata
{
    public static AuthenticationAssuranceFilterRegistrationMetadata Instance { get; } = new();

    private AuthenticationAssuranceFilterRegistrationMetadata()
    {
    }
}
