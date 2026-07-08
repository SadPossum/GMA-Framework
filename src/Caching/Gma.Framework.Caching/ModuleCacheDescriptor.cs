namespace Gma.Framework.Caching;

using Gma.Framework.Modules;

public sealed record ModuleCacheDescriptor
{
    public ModuleCacheDescriptor(string name, CacheScope scope, IReadOnlyList<string> tags)
    {
        this.Name = CacheIdentity.ValidateName(name, nameof(name));
        this.Scope = scope is CacheScope.Unknown || !Enum.IsDefined(scope)
            ? throw new ArgumentException("Cache scope must be a known non-unknown value.", nameof(scope))
            : scope;
        this.TenantScoped = this.Scope == CacheScope.Tenant;
        this.Tags = Array.AsReadOnly((tags ?? throw new ArgumentNullException(nameof(tags)))
            .Select(tag => CacheIdentity.ValidateName(tag, nameof(tags)))
            .ToArray());

        if (this.Tags.Count == 0)
        {
            throw new ArgumentException("Cache metadata must declare at least one tag.", nameof(tags));
        }

        ModuleMetadataGuards.EnsureUnique(this.Tags, tag => tag, "cache tag");
    }

    public string Name { get; }
    public CacheScope Scope { get; }
    public bool TenantScoped { get; }
    public IReadOnlyList<string> Tags { get; }
}
