namespace Gma.Framework.Caching.Infrastructure;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Gma.Framework.Caching;
using Gma.Framework.Runtime;

internal sealed class CacheKeyFormatter(
    ICacheScopeValueResolver scopeValueResolver,
    IHostEnvironment environment,
    IOptions<CachingOptions> options,
    IOptions<ApplicationIdentityOptions> applicationIdentity)
{
    private readonly CachingOptions cachingOptions = options.Value;
    private readonly string applicationNamespace = applicationIdentity.Value.EffectiveNamespace;

    public string Format(CacheKey key) => this.Format(key.Module, key.Entry, key.Scope, key.Segments);

    public string Format(CacheTag tag) => this.Format(tag.Module, tag.Entry, tag.Scope, tag.Segments);

    private string Format(string module, string entry, CacheScope scope, IReadOnlyList<string> segments)
    {
        string scopeValue = scopeValueResolver.Resolve(scope);
        string scopeName = scope.ToString().ToLowerInvariant();
        IEnumerable<string> parts = new[]
        {
            CacheStorageIdentifiers.NormalizeKeyPrefix(
                string.IsNullOrWhiteSpace(this.cachingOptions.KeyPrefix)
                    ? this.applicationNamespace
                    : this.cachingOptions.KeyPrefix),
            CacheStorageIdentifiers.NormalizeEnvironmentName(environment.EnvironmentName),
            module,
            scopeName,
            scopeValue,
            entry
        }.Concat(segments).Select(Uri.EscapeDataString);

        string physicalKey = string.Join(':', parts);

        if (physicalKey.Length > this.cachingOptions.MaximumKeyLength)
        {
            throw new InvalidOperationException(
                $"The generated cache key exceeds the configured {this.cachingOptions.MaximumKeyLength}-character limit.");
        }

        return physicalKey;
    }
}
