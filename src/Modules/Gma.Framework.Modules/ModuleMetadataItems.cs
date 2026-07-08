namespace Gma.Framework.Modules;

public sealed record ModuleMetadataItems
{
    public static readonly ModuleMetadataItems Empty = new([]);
    private readonly ModuleMetadataItem[] sortedItems;

    private ModuleMetadataItems(IReadOnlyList<ModuleMetadataItem> items)
    {
        this.Items = ModuleMetadataGuards.CopyOptionalList(items);
        ModuleMetadataGuards.EnsureUnique(this.Items, item => item.Key, "metadata item");
        this.sortedItems = this.Items
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<ModuleMetadataItem> Items { get; }

    public bool IsEmpty => this.Items.Count == 0;

    public static ModuleMetadataItems Create(IReadOnlyList<ModuleMetadataItem>? items) =>
        items is null || items.Count == 0
            ? Empty
            : new ModuleMetadataItems(items);

    public TItem? Get<TItem>()
        where TItem : ModuleMetadataItem =>
        this.Items.OfType<TItem>().SingleOrDefault();

    public bool Contains<TItem>()
        where TItem : ModuleMetadataItem =>
        this.Get<TItem>() is not null;

    public bool Equals(ModuleMetadataItems? other) =>
        other is not null &&
        this.sortedItems.SequenceEqual(other.sortedItems);

    public override int GetHashCode()
    {
        HashCode hash = new();
        foreach (ModuleMetadataItem item in this.sortedItems)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }
}
