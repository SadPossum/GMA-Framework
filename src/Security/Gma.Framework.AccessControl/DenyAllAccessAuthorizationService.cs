namespace Gma.Framework.AccessControl;

public sealed class DenyAllAccessAuthorizationService : IAccessAuthorizationService
{
    public Task<AccessDecision> AuthorizeAsync(
        AccessRequirement requirement,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requirement);

        return Task.FromResult(AccessDecision.Denied(
            AccessDecisionReasonCodes.DenyByDefault,
            "Access control is not configured."));
    }
}
