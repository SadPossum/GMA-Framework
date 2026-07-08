namespace Gma.Framework.Caching.Infrastructure;

using System.Diagnostics.CodeAnalysis;
using Gma.Framework.Caching;

internal static class CacheStorageIdentifiers
{
    public const int EnvironmentNameMaxLength = 64;

    public static string NormalizeKeyPrefix(string? keyPrefix)
    {
        return NormalizeLowercaseIdentifier(
            keyPrefix,
            "Caching:KeyPrefix",
            CachingOptions.KeyPrefixMaxLength);
    }

    public static string NormalizeEnvironmentName(string? environmentName)
    {
        return NormalizeLowercaseIdentifier(
            environmentName,
            "Host environment name",
            EnvironmentNameMaxLength);
    }

    public static bool IsValidKeyPrefix(string? keyPrefix) =>
        TryNormalizeLowercaseIdentifier(keyPrefix, CachingOptions.KeyPrefixMaxLength, out _);

    private static string NormalizeLowercaseIdentifier(
        string? value,
        string description,
        int maxLength)
    {
        if (TryNormalizeLowercaseIdentifier(value, maxLength, out string? normalized))
        {
            return normalized;
        }

        throw new ArgumentException(
            $"{description} must be 1-{maxLength} characters and use only ASCII letters, digits, '-' or '_'.",
            nameof(value));
    }

    private static bool TryNormalizeLowercaseIdentifier(
        string? value,
        int maxLength,
        [NotNullWhen(true)]
        out string? normalized)
    {
        normalized = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string candidate = value.Trim().ToLowerInvariant();
        if (candidate.Length > maxLength ||
            !candidate.All(character =>
                character is (>= 'a' and <= 'z') or
                    (>= '0' and <= '9') or
                    '-' or '_'))
        {
            return false;
        }

        normalized = candidate;
        return true;
    }
}
