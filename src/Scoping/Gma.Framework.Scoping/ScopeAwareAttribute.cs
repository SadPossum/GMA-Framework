namespace Gma.Framework.Scoping;

using Gma.Framework.Modules;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class ScopeAwareAttribute : Attribute, IModuleMetadataContributor
{
    public ModuleMetadataItem CreateMetadataItem() => ScopeMetadataItem.Instance;
}
