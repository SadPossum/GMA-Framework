namespace Gma.Framework.Caching;

public sealed class CachingOptions
{
    public const string SectionName = "Caching";
    public const int KeyPrefixMaxLength = 32;

    public bool Enabled { get; set; }
    public CacheProvider Provider { get; set; } = CacheProvider.Memory;
    public TimeSpan DefaultDistributedExpiration { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan DefaultLocalExpiration { get; set; } = TimeSpan.FromSeconds(30);
    public int MaximumPayloadBytes { get; set; } = 1024 * 1024;
    public int MaximumKeyLength { get; set; } = 1024;
    public string? KeyPrefix { get; set; }
}
