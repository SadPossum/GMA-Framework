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

    public Task<IReadOnlyList<AccessDecision>> AuthorizeManyAsync(
        IReadOnlyList<AccessRequirement> requirements,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        if (requirements.Any(requirement => requirement is null))
        {
            throw new ArgumentException(
                "Authorization requirements cannot contain null values.",
                nameof(requirements));
        }

        IReadOnlyList<AccessDecision> decisions = requirements
            .Select(_ => AccessDecision.Denied(
                AccessDecisionReasonCodes.DenyByDefault,
                "Access control is not configured."))
            .ToArray();
        return Task.FromResult(decisions);
    }
}
