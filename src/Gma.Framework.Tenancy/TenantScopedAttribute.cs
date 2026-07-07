namespace Gma.Framework.Tenancy;

using Gma.Framework.Modules;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class TenantScopedAttribute : Attribute, IModuleMetadataContributor
{
    public ModuleMetadataItem CreateMetadataItem() => TenantScopeMetadataItem.Instance;
}
