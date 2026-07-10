namespace Gma.Framework.AccessControl.AspNetCore;

using Gma.Framework.AccessControl;
using Gma.Framework.Permissions;

public sealed record AccessPermissionMetadata
{
    public AccessPermissionMetadata(
        PermissionCode permission,
        AccessScope? scope = null,
        string? scopeResolverName = null,
        string? scopeSegmentName = null,
        string? scopeRouteValueName = null,
        bool requireScope = false)
    {
        ArgumentNullException.ThrowIfNull(permission);

        this.Permission = permission;
        this.Scope = scope;
        this.ScopeResolverName = NormalizeIdentifier(scopeResolverName, nameof(scopeResolverName));
        this.ScopeSegmentName = NormalizeIdentifier(scopeSegmentName, nameof(scopeSegmentName));
        this.ScopeRouteValueName = NormalizeIdentifier(scopeRouteValueName, nameof(scopeRouteValueName));
        this.RequireScope = requireScope;

        if (this.ScopeSegmentName is not null && this.ScopeRouteValueName is null)
        {
            throw new ArgumentException("A route value name is required when a route scope segment is configured.", nameof(scopeRouteValueName));
        }

        if (this.ScopeSegmentName is null && this.ScopeRouteValueName is not null)
        {
            throw new ArgumentException("A scope segment name is required when a route value name is configured.", nameof(scopeSegmentName));
        }
    }

    public PermissionCode Permission { get; }
    public AccessScope? Scope { get; }
    public string? ScopeResolverName { get; }
    public string? ScopeSegmentName { get; }
    public string? ScopeRouteValueName { get; }
    public bool RequireScope { get; }

    private static string? NormalizeIdentifier(string? name, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        string normalized = name.Trim();
        if (normalized.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            throw new ArgumentException("Access-control metadata identifiers cannot contain whitespace or control characters.", parameterName);
        }

        return normalized;
    }
}
