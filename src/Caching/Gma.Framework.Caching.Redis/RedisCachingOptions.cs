namespace Gma.Framework.Caching.Redis;

public sealed class RedisCachingOptions
{
    public const string SectionName = "Caching:Redis";
    public const int ConnectionNameMaxLength = 64;
    public const int InstanceNameMaxLength = 64;

    public string ConnectionName { get; set; } = "redis";
    public string InstanceName { get; set; } = string.Empty;
}
