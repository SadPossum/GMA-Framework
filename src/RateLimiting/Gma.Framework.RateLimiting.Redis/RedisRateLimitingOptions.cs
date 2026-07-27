namespace Gma.Framework.RateLimiting.Redis;

public sealed class RedisRateLimitingOptions
{
    public const string SectionName = "RateLimiting:Redis";
    public const int ConnectionNameMaxLength = 64;
    public const int InstanceNameMaxLength = 64;

    public string ConnectionName { get; set; } = "redis";
    public string InstanceName { get; set; } = string.Empty;
}
