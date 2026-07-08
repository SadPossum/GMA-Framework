namespace Gma.Framework.Caching.Infrastructure;

using Microsoft.Extensions.Options;
using Gma.Framework.Caching;

internal sealed class CachingOptionsValidator : IValidateOptions<CachingOptions>
{
    public ValidateOptionsResult Validate(string? name, CachingOptions options)
    {
        if (!Enum.IsDefined(options.Provider) || options.Provider == CacheProvider.Unknown)
        {
            return ValidateOptionsResult.Fail("Caching:Provider is not supported.");
        }

        if (options.DefaultDistributedExpiration <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail("Caching:DefaultDistributedExpiration must be positive.");
        }

        if (options.DefaultLocalExpiration <= TimeSpan.Zero ||
            options.DefaultLocalExpiration > options.DefaultDistributedExpiration)
        {
            return ValidateOptionsResult.Fail(
                "Caching:DefaultLocalExpiration must be positive and no greater than the distributed expiration.");
        }

        if (options.MaximumPayloadBytes <= 0)
        {
            return ValidateOptionsResult.Fail("Caching:MaximumPayloadBytes must be positive.");
        }

        if (options.MaximumKeyLength <= 0)
        {
            return ValidateOptionsResult.Fail("Caching:MaximumKeyLength must be positive.");
        }

        if (!string.IsNullOrWhiteSpace(options.KeyPrefix) &&
            !CacheStorageIdentifiers.IsValidKeyPrefix(options.KeyPrefix))
        {
            return ValidateOptionsResult.Fail(
                $"Caching:KeyPrefix must be 1-{CachingOptions.KeyPrefixMaxLength} characters and use only ASCII letters, digits, '-' or '_'.");
        }

        return ValidateOptionsResult.Success;
    }
}
