namespace Gma.Framework.AccessControl;

public interface IAccessDecisionProvider
{
    Task<AccessDecision> DecideAsync(AccessRequirement requirement, CancellationToken cancellationToken);

    async Task<IReadOnlyList<AccessDecision>> DecideManyAsync(
        IReadOnlyList<AccessRequirement> requirements,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        AccessDecision[] decisions = new AccessDecision[requirements.Count];
        for (int index = 0; index < requirements.Count; index++)
        {
            AccessRequirement requirement = requirements[index] ?? throw new ArgumentException(
                "Authorization requirements cannot contain null values.",
                nameof(requirements));
            decisions[index] = await this.DecideAsync(requirement, cancellationToken).ConfigureAwait(false);
        }

        return decisions;
    }
}
