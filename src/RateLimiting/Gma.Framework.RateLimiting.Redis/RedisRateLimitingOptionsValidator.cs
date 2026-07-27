namespace Gma.Framework.RateLimiting.Redis;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

internal sealed class RedisRateLimitingOptionsValidator(
    IConfiguration configuration)
    : IValidateOptions<RedisRateLimitingOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        RedisRateLimitingOptions options)
    {
        if (!IsValidConnectionName(options.ConnectionName))
        {
            return ValidateOptionsResult.Fail(
                $"{RedisRateLimitingOptions.SectionName}:ConnectionName must be 1-{RedisRateLimitingOptions.ConnectionNameMaxLength} characters and cannot contain whitespace or control characters.");
        }

        if (!IsValidInstanceName(options.InstanceName))
        {
            return ValidateOptionsResult.Fail(
                $"{RedisRateLimitingOptions.SectionName}:InstanceName must be empty or use at most {RedisRateLimitingOptions.InstanceNameMaxLength} ASCII letters, digits, '-' or '_'.");
        }

        string? connectionString =
            configuration.GetConnectionString(options.ConnectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return ValidateOptionsResult.Fail(
                $"ConnectionStrings:{options.ConnectionName} is required when Redis rate limiting is enabled.");
        }

        try
        {
            _ = ConfigurationOptions.Parse(connectionString);
        }
        catch (Exception exception) when (
            exception is FormatException or ArgumentException)
        {
            return ValidateOptionsResult.Fail(
                $"ConnectionStrings:{options.ConnectionName} must be a valid Redis connection string.");
        }

        return ValidateOptionsResult.Success;
    }

    private static bool IsValidConnectionName(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= RedisRateLimitingOptions.ConnectionNameMaxLength &&
        value.All(character =>
            !char.IsWhiteSpace(character) && !char.IsControl(character));

    private static bool IsValidInstanceName(string? value) =>
        value is not null &&
        value.Length <= RedisRateLimitingOptions.InstanceNameMaxLength &&
        value.All(character =>
            character is (>= 'a' and <= 'z') or
                (>= 'A' and <= 'Z') or
                (>= '0' and <= '9') or
                '-' or '_');
}
