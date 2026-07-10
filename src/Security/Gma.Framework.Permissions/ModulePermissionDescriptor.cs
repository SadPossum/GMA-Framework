namespace Gma.Framework.Permissions;

public sealed record ModulePermissionDescriptor
{
    public ModulePermissionDescriptor(
        string code,
        string description,
        PermissionScopeRequirement scopeRequirement = PermissionScopeRequirement.Global)
        : this(PermissionCode.Create(code), description, scopeRequirement)
    {
    }

    public ModulePermissionDescriptor(
        PermissionCode code,
        string description,
        PermissionScopeRequirement scopeRequirement = PermissionScopeRequirement.Global)
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
    }

    public PermissionCode Permission { get; }
    public string Code { get; }
    public string Description { get; }
    public PermissionScopeRequirement ScopeRequirement { get; }
}
