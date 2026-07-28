namespace Gma.Framework.Api.Production;

using System.Security.Cryptography;
using System.Text;
using Gma.Framework.RateLimiting;
using Microsoft.AspNetCore.Http;

internal static class HttpRateLimitPolicy
{
    public static bool IsSensitive(
        HttpContext context,
        RateLimitingSettings options) =>
        options.SensitivePathPrefixes.Any(prefix =>
            context.Request.Path.StartsWithSegments(
                prefix,
                StringComparison.OrdinalIgnoreCase));

    public static string ClientPartition(HttpContext context)
    {
        string client = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(client));
        return Convert.ToHexStringLower(digest);
    }

    public static MultiPartitionRateLimitRequest CreateDistributedRequest(
        HttpContext context,
        RateLimitingSettings options)
    {
        string clientPartition = ClientPartition(context);
        TimeSpan window = TimeSpan.FromSeconds(options.WindowSeconds);
        List<FixedWindowRateLimitPartition> partitions =
        [
            new("http-general", options.GlobalPermitLimit, window)
        ];

        if (IsSensitive(context, options))
        {
            partitions.Add(
                new FixedWindowRateLimitPartition(
                    "http-sensitive",
                    options.SensitivePermitLimit,
                    window));
        }

        return new MultiPartitionRateLimitRequest(
            $"http-{clientPartition}",
            permitCount: 1,
            partitions);
    }
}
