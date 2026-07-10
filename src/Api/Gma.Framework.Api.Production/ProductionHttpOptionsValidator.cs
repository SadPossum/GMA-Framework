namespace Gma.Framework.Api.Production;

using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

internal sealed class ProductionHttpOptionsValidator(
    IHostEnvironment environment,
    IConfiguration configuration) : IValidateOptions<ProductionHttpOptions>
{
    public ValidateOptionsResult Validate(string? name, ProductionHttpOptions options)
    {
        string[] failures = ProductionHttpOptionsValidation.Validate(
            options,
            environment.IsDevelopment(),
            configuration["AllowedHosts"]);

        return failures.Length == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

internal static class ProductionHttpOptionsValidation
{
    public static string[] Validate(
        ProductionHttpOptions options,
        bool isDevelopment,
        string? allowedHosts)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        if (!isDevelopment &&
            !options.AllowAnyHost &&
            IsUnrestrictedAllowedHosts(allowedHosts))
        {
            failures.Add(
                "AllowedHosts must list the production host names. Set Http:AllowAnyHost=true only when unrestricted host filtering is intentional.");
        }

        ValidateForwardedHeaders(options.ForwardedHeaders, failures);
        ValidateCors(options.Cors, failures);
        if (options.PrivateNetwork.AllowedNetworks.Any(value => !IPNetwork.TryParse(value, out _)))
        {
            failures.Add("Http:PrivateNetwork:AllowedNetworks must contain valid CIDR networks.");
        }

        if (options.RequestTimeouts.Enabled &&
            options.RequestTimeouts.DefaultTimeoutSeconds is < 1 or > 600)
        {
            failures.Add("Http:RequestTimeouts:DefaultTimeoutSeconds must be between 1 and 600.");
        }

        if (options.RateLimiting.Enabled)
        {
            if (options.RateLimiting.GlobalPermitLimit is < 1 or > 1_000_000)
            {
                failures.Add("Http:RateLimiting:GlobalPermitLimit must be between 1 and 1000000.");
            }

            if (options.RateLimiting.SensitivePermitLimit is < 1 or > 100_000)
            {
                failures.Add("Http:RateLimiting:SensitivePermitLimit must be between 1 and 100000.");
            }

            if (options.RateLimiting.SensitivePermitLimit > options.RateLimiting.GlobalPermitLimit)
            {
                failures.Add("Http:RateLimiting:SensitivePermitLimit cannot exceed GlobalPermitLimit.");
            }

            if (options.RateLimiting.WindowSeconds is < 1 or > 3600)
            {
                failures.Add("Http:RateLimiting:WindowSeconds must be between 1 and 3600.");
            }

            string[] invalidPrefixes = options.RateLimiting.SensitivePathPrefixes
                .Where(path => string.IsNullOrWhiteSpace(path) || !path.StartsWith('/'))
                .ToArray();
            if (invalidPrefixes.Length > 0)
            {
                failures.Add("Http:RateLimiting:SensitivePathPrefixes must contain nonblank absolute application paths.");
            }
        }

        return [.. failures];
    }

    private static bool IsUnrestrictedAllowedHosts(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
        value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(host => string.Equals(host, "*", StringComparison.Ordinal));

    private static void ValidateForwardedHeaders(
        ForwardedHeadersSettings options,
        List<string> failures)
    {
        if (!options.Enabled)
        {
            return;
        }

        if (options.ForwardLimit is < 1 or > 10)
        {
            failures.Add("Http:ForwardedHeaders:ForwardLimit must be between 1 and 10.");
        }

        string[] invalidProxies = options.KnownProxies
            .Where(value => !IPAddress.TryParse(value, out _))
            .ToArray();
        if (invalidProxies.Length > 0)
        {
            failures.Add("Http:ForwardedHeaders:KnownProxies must contain valid IP addresses.");
        }

        if (!options.AllowUnknownProxies && options.KnownProxies.Length == 0)
        {
            failures.Add(
                "Http:ForwardedHeaders requires at least one KnownProxies entry unless AllowUnknownProxies is explicitly enabled.");
        }
    }

    private static void ValidateCors(CorsSettings options, List<string> failures)
    {
        if (!options.Enabled)
        {
            return;
        }

        if (options.AllowedOrigins.Length == 0)
        {
            failures.Add("Http:Cors:AllowedOrigins is required when CORS is enabled.");
            return;
        }

        foreach (string origin in options.AllowedOrigins)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri) ||
                (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(uri.PathAndQuery) && uri.PathAndQuery != "/"))
            {
                failures.Add($"Http:Cors:AllowedOrigins contains invalid origin '{origin}'.");
            }
        }
    }
}
