namespace Gma.Framework.AccessControl;

public static class AccessScopeMatcher
{
    public static bool GrantSatisfiesRequest(
        AccessScope grantScope,
        AccessScope requestedScope,
        AccessScopeMatchOptions options)
    {
        ArgumentNullException.ThrowIfNull(grantScope);
        ArgumentNullException.ThrowIfNull(requestedScope);
        ArgumentNullException.ThrowIfNull(options);

        if (string.Equals(grantScope.Value, requestedScope.Value, StringComparison.Ordinal))
        {
            return true;
        }

        if (grantScope.IsGlobal)
        {
            return options.AllowGlobalScopeGrant;
        }

        if (!options.AllowAncestorScopeGrants)
        {
            return false;
        }

        if (grantScope.Segments.Count >= requestedScope.Segments.Count)
        {
            return false;
        }

        for (int index = 0; index < grantScope.Segments.Count; index++)
        {
            AccessScopeSegment grantSegment = grantScope.Segments[index];
            AccessScopeSegment requestedSegment = requestedScope.Segments[index];
            if (!string.Equals(grantSegment.Name, requestedSegment.Name, StringComparison.Ordinal) ||
                !string.Equals(grantSegment.Value, requestedSegment.Value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
