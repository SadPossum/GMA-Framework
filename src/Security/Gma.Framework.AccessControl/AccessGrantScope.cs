namespace Gma.Framework.AccessControl;

public sealed record AccessGrantScope
{
    public AccessGrantScope(AccessScope scope, AccessScopeMatchOptions matchOptions)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(matchOptions);

        this.Scope = scope;
        this.MatchOptions = matchOptions;
    }

    public AccessScope Scope { get; }
    public AccessScopeMatchOptions MatchOptions { get; }

    public bool Grants(AccessScope requestedScope) =>
        AccessScopeMatcher.GrantSatisfiesRequest(this.Scope, requestedScope, this.MatchOptions);
}
