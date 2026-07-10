namespace Gma.Framework.AccessControl;

public interface IAccessDecisionProvider
{
    Task<AccessDecision> DecideAsync(AccessRequirement requirement, CancellationToken cancellationToken);
}
