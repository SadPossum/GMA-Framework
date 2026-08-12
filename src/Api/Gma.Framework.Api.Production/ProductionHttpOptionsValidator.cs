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
            if (!Enum.IsDefined(options.RateLimiting.Mode))
            {
                failures.Add(
                    "Http:RateLimiting:Mode must be InProcess or Distributed.");
            }

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
                .Where(path => !IsValidPathPrefix(path))
                .ToArray();
            if (invalidPrefixes.Length > 0)
            {
                failures.Add("Http:RateLimiting:SensitivePathPrefixes must contain nonblank absolute application paths.");
            }

            ValidateRateLimitPolicies(options.RateLimiting, failures);
        }

        return [.. failures];
    }

    private static void ValidateRateLimitPolicies(
        RateLimitingSettings options,
        List<string> failures)
    {
        if (options.Policies.Length > RateLimitingSettings.MaximumAdditionalPolicies)
        {
            failures.Add(
                $"Http:RateLimiting:Policies cannot contain more than {RateLimitingSettings.MaximumAdditionalPolicies} policies.");
        }

        string[] duplicateNames = options.Policies
            .Where(policy => policy is not null)
            .GroupBy(policy => policy.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateNames.Length > 0)
        {
            failures.Add("Http:RateLimiting:Policies must use unique names.");
        }

        foreach (HttpRateLimitPolicySettings? policy in options.Policies)
        {
            if (policy is null)
            {
                failures.Add("Http:RateLimiting:Policies cannot contain null entries.");
                continue;
            }

            if (!IsValidPolicyName(policy.Name))
            {
                failures.Add(
                    "Http:RateLimiting:Policies names must be 1-64 lowercase letters, digits, or hyphens and start with a letter or digit.");
            }

            if (policy.PermitLimit is < 1 or > 100_000)
            {
                failures.Add(
                    $"Http:RateLimiting:Policies:{policy.Name}:PermitLimit must be between 1 and 100000.");
            }

            if (policy.PathPrefixes.Length == 0 ||
                policy.PathPrefixes.Any(path => !IsValidPathPrefix(path)))
            {
                failures.Add(
                    $"Http:RateLimiting:Policies:{policy.Name}:PathPrefixes must contain nonblank absolute application paths.");
            }

            if (policy.Methods.Any(method => !StandardHttpMethods.Contains(method)))
            {
                failures.Add(
                    $"Http:RateLimiting:Policies:{policy.Name}:Methods must contain uppercase standard HTTP methods.");
            }

            if (policy.Methods.Distinct(StringComparer.Ordinal).Count() != policy.Methods.Length)
            {
                failures.Add(
                    $"Http:RateLimiting:Policies:{policy.Name}:Methods cannot contain duplicates.");
            }
        }

        int maximumMatchingPolicyCount = options.Policies.Length +
            (options.SensitivePathPrefixes.Length > 0 ? 1 : 0);
        if (maximumMatchingPolicyCount >=
            Gma.Framework.RateLimiting.MultiPartitionRateLimitRequest.MaxPartitions)
        {
            failures.Add(
                $"Http:RateLimiting allows at most {Gma.Framework.RateLimiting.MultiPartitionRateLimitRequest.MaxPartitions - 1} combined legacy and named policies so the global budget remains atomic.");
        }
    }

    private static bool IsValidPathPrefix(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        path.StartsWith('/') &&
        !path.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)) &&
        !path.Contains('?') &&
        !path.Contains('#');

    private static bool IsValidPolicyName(string? name) =>
        !string.IsNullOrWhiteSpace(name) &&
        name.Length <= 64 &&
        char.IsAsciiLetterOrDigit(name[0]) &&
        name.All(character =>
            char.IsAsciiLetterLower(character) ||
            char.IsAsciiDigit(character) ||
            character == '-');

    private static readonly HashSet<string> StandardHttpMethods = new(
        [
            "CONNECT",
            "DELETE",
            "GET",
            "HEAD",
            "OPTIONS",
            "PATCH",
            "POST",
            "PUT",
            "TRACE"
        ],
        StringComparer.Ordinal);

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

        string[] invalidNetworks = options.KnownNetworks
            .Where(value => !IPNetwork.TryParse(value, out _))
            .ToArray();
        if (invalidNetworks.Length > 0)
        {
            failures.Add(
                "Http:ForwardedHeaders:KnownNetworks must contain valid CIDR networks.");
        }

        if (options.AllowUnknownProxies &&
            (options.KnownProxies.Length > 0 || options.KnownNetworks.Length > 0))
        {
            failures.Add(
                "Http:ForwardedHeaders cannot combine AllowUnknownProxies with KnownProxies or KnownNetworks.");
        }

        if (!options.AllowUnknownProxies &&
            options.KnownProxies.Length == 0 &&
            options.KnownNetworks.Length == 0)
        {
            failures.Add(
                "Http:ForwardedHeaders requires at least one KnownProxies or KnownNetworks entry unless AllowUnknownProxies is explicitly enabled.");
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
