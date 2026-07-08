namespace Gma.Framework.Authorization;

public sealed record ModulePermissionDescriptor
{
    public ModulePermissionDescriptor(string code, string description, bool tenantScoped)
    {
        this.Code = AuthorizationMetadataNaming.NormalizePermissionCode(code, nameof(code));
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        this.Description = description.Trim();
        this.TenantScoped = tenantScoped;
    }

    public string Code { get; }
    public string Description { get; }
    public bool TenantScoped { get; }
}
