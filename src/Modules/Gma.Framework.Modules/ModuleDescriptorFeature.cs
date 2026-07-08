namespace Gma.Framework.Modules;

public abstract record ModuleDescriptorFeature
{
    protected ModuleDescriptorFeature(string key)
        => this.Key = ModuleMetadataNaming.NormalizeFeatureKey(key, nameof(key));

    public string Key { get; }

    public virtual void Validate(ModuleDescriptorFeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }
}
