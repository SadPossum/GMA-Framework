namespace Gma.Framework.Scoping;

using Gma.Framework.Modules;

public sealed record ScopeMetadataItem : ModuleMetadataItem
{
    public const string MetadataKey = "scoping.scope";
    public static readonly ScopeMetadataItem Instance = new();

    private ScopeMetadataItem()
        : base(MetadataKey)
    {
    }
}
