namespace Gma.Framework.AccessControl;

internal sealed class AccessAuthorizationService(IEnumerable<IAccessDecisionProvider> providers)
    : IAccessAuthorizationService
{
    public async Task<AccessDecision> AuthorizeAsync(
        AccessRequirement requirement,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requirement);

        AccessDecision? allowDecision = null;
        foreach (IAccessDecisionProvider provider in providers)
        {
            AccessDecision decision = await provider
                .DecideAsync(requirement, cancellationToken)
                .ConfigureAwait(false);

            if (decision.IsDenied)
            {
                return decision;
            }

            if (decision.IsAllowed)
            {
                allowDecision ??= decision;
            }
        }

        return allowDecision ?? AccessDecision.Denied(
            AccessDecisionReasonCodes.DenyByDefault,
            "No access-control provider allowed the requirement.");
    }
}
