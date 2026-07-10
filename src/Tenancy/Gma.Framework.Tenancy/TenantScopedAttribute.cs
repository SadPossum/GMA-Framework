namespace Gma.Framework.Tenancy;

using Gma.Framework.Modules;
using Gma.Framework.Scoping;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class TenantScopedAttribute : Attribute, IModuleMetadataContributor
{
    public ModuleMetadataItem CreateMetadataItem() => ScopeMetadataItem.Instance;
}
