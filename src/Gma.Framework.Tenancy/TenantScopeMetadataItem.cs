namespace Gma.Framework.Tenancy;

using Gma.Framework.Modules;

public sealed record TenantScopeMetadataItem : ModuleMetadataItem
{
    public static readonly TenantScopeMetadataItem Instance = new();

    private TenantScopeMetadataItem()
        : base("tenancy.scope")
    {
    }
}
