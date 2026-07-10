namespace Gma.Framework.Permissions;

public sealed record ModulePermissionDescriptor
{
    public ModulePermissionDescriptor(
        string code,
        string description,
        PermissionScopeRequirement scopeRequirement = PermissionScopeRequirement.Global,
        PermissionScopeGrantPolicy? scopeGrantPolicy = null)
        : this(PermissionCode.Create(code), description, scopeRequirement, scopeGrantPolicy)
    {
    }

    public ModulePermissionDescriptor(
        PermissionCode code,
        string description,
        PermissionScopeRequirement scopeRequirement = PermissionScopeRequirement.Global,
        PermissionScopeGrantPolicy? scopeGrantPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (scopeRequirement == PermissionScopeRequirement.Unknown || !Enum.IsDefined(scopeRequirement))
        {
            throw new ArgumentException(
                "Permission scope requirement must be a defined non-unknown value.",
                nameof(scopeRequirement));
        }

        this.Permission = code;
        this.Code = code.Value;
        this.Description = description.Trim();
        this.ScopeRequirement = scopeRequirement;
        this.ScopeGrantPolicy = scopeGrantPolicy ?? PermissionScopeGrantPolicy.Exact;
    }

    public PermissionCode Permission { get; }
    public string Code { get; }
    public string Description { get; }
    public PermissionScopeRequirement ScopeRequirement { get; }
    public PermissionScopeGrantPolicy ScopeGrantPolicy { get; }
}
