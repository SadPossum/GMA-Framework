namespace Gma.Framework.Modules;

public abstract record ModuleMetadataItem
{
    protected ModuleMetadataItem(string key)
        => this.Key = ModuleMetadataNaming.NormalizeFeatureKey(key, nameof(key));

    public string Key { get; }
}
