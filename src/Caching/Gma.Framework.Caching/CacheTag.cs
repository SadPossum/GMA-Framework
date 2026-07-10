namespace Gma.Framework.Caching;

public sealed record CacheTag
{
    public const int MaxSegments = CacheIdentity.MaxSegments;
    public const int SegmentMaxLength = CacheIdentity.SegmentMaxLength;

    private CacheTag(string module, string entry, CacheScope scope, IEnumerable<string>? segments)
    {
        this.Module = CacheIdentity.ValidateModuleName(module, nameof(module));
        this.Entry = CacheIdentity.ValidateName(entry, nameof(entry));
        this.Scope = scope;
        this.Segments = CacheIdentity.ValidateSegments(segments);
    }

    public string Module { get; }
    public string Entry { get; }
    public CacheScope Scope { get; }
    public IReadOnlyList<string> Segments { get; }

    public static CacheTag Scoped(string module, string entry, params string[] segments) =>
        new(module, entry, CacheScope.Scope, segments);

    public static CacheTag Global(string module, string entry, params string[] segments) =>
        new(module, entry, CacheScope.Global, segments);
}
