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

    public async Task<IReadOnlyList<AccessDecision>> AuthorizeManyAsync(
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

        if (requirements.Count == 0)
        {
            return [];
        }

        AccessDecision?[] allowDecisions = new AccessDecision?[requirements.Count];
        AccessDecision?[] denyDecisions = new AccessDecision?[requirements.Count];
        foreach (IAccessDecisionProvider provider in providers)
        {
            int[] activeIndexes = Enumerable.Range(0, requirements.Count)
                .Where(index => denyDecisions[index] is null)
                .ToArray();
            if (activeIndexes.Length == 0)
            {
                break;
            }

            AccessRequirement[] activeRequirements = activeIndexes
                .Select(index => requirements[index])
                .ToArray();
            IReadOnlyList<AccessDecision>? providerDecisions = await provider
                .DecideManyAsync(activeRequirements, cancellationToken)
                .ConfigureAwait(false);
            if (providerDecisions is null || providerDecisions.Count != activeRequirements.Length)
            {
                throw new InvalidOperationException(
                    $"Access decision provider '{provider.GetType().FullName}' returned " +
                    $"{providerDecisions?.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "a null result"} " +
                    $"for {activeRequirements.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)} requirements.");
            }

            for (int resultIndex = 0; resultIndex < providerDecisions.Count; resultIndex++)
            {
                AccessDecision decision = providerDecisions[resultIndex] ?? throw new InvalidOperationException(
                    $"Access decision provider '{provider.GetType().FullName}' returned a null decision.");
                int requirementIndex = activeIndexes[resultIndex];
                if (decision.IsDenied)
                {
                    denyDecisions[requirementIndex] = decision;
                }
                else if (decision.IsAllowed)
                {
                    allowDecisions[requirementIndex] ??= decision;
                }
            }
        }

        AccessDecision[] results = new AccessDecision[requirements.Count];
        for (int index = 0; index < results.Length; index++)
        {
            results[index] = denyDecisions[index] ?? allowDecisions[index] ?? AccessDecision.Denied(
                AccessDecisionReasonCodes.DenyByDefault,
                "No access-control provider allowed the requirement.");
        }

        return results;
    }
}
