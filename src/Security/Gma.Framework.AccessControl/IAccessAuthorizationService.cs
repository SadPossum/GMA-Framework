namespace Gma.Framework.AccessControl;

public interface IAccessAuthorizationService
{
    Task<AccessDecision> AuthorizeAsync(AccessRequirement requirement, CancellationToken cancellationToken);
}
