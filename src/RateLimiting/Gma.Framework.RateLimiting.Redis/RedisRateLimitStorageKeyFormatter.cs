namespace Gma.Framework.RateLimiting.Redis;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Gma.Framework.RateLimiting;
using Gma.Framework.Runtime;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

internal sealed class RedisRateLimitStorageKeyFormatter
{
    private const int EnvironmentNameMaxLength = 64;
    private readonly string prefix;

    public RedisRateLimitStorageKeyFormatter(
        IOptions<RedisRateLimitingOptions> options,
        IOptions<ApplicationIdentityOptions> applicationIdentity,
        IHostEnvironment environment)
    {
        string instance = string.IsNullOrWhiteSpace(options.Value.InstanceName)
            ? applicationIdentity.Value.EffectiveNamespace
            : NormalizeStorageIdentifier(
                options.Value.InstanceName,
                RedisRateLimitingOptions.InstanceNameMaxLength,
                "Redis rate-limit instance name");
        string environmentName = NormalizeStorageIdentifier(
            environment.EnvironmentName,
            EnvironmentNameMaxLength,
            "Host environment name");

        this.prefix = $"{instance}:{environmentName}:rate-limit";
    }

    public RedisKey Format(
        string atomicGroup,
        FixedWindowRateLimitPartition partition)
    {
        string groupHash = Hash(atomicGroup);
        long windowMilliseconds = checked((long)partition.Window.TotalMilliseconds);
        string partitionDescriptor = string.Create(
            CultureInfo.InvariantCulture,
            $"{partition.Identity}|{partition.PermitLimit}|{windowMilliseconds}");
        string partitionHash = Hash(partitionDescriptor);

        return $"{this.prefix}:{{{groupHash}}}:{partitionHash}";
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

    private static string NormalizeStorageIdentifier(
        string? value,
        int maxLength,
        string description)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{description} is required.", nameof(value));
        }

        string normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > maxLength ||
            normalized.Any(character =>
                character is not (>= 'a' and <= 'z') and
                not (>= '0' and <= '9') and
                not '-' and
                not '_'))
        {
            throw new ArgumentException(
                $"{description} must be 1-{maxLength} characters and use only ASCII letters, digits, '-' or '_'.",
                nameof(value));
        }

        return normalized;
    }
}
