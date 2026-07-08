namespace Gma.Framework.Caching.Redis;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

internal sealed class RedisCachingOptionsValidator(IConfiguration configuration) : IValidateOptions<RedisCachingOptions>
{
    public ValidateOptionsResult Validate(string? name, RedisCachingOptions options)
    {
        if (!IsValidConnectionName(options.ConnectionName))
        {
            return ValidateOptionsResult.Fail(
                $"{RedisCachingOptions.SectionName}:ConnectionName must be 1-{RedisCachingOptions.ConnectionNameMaxLength} characters and cannot contain whitespace or control characters.");
        }

        if (!IsValidInstanceName(options.InstanceName))
        {
            return ValidateOptionsResult.Fail(
                $"{RedisCachingOptions.SectionName}:InstanceName must be {RedisCachingOptions.InstanceNameMaxLength} characters or fewer and cannot contain whitespace or control characters.");
        }

        string? connectionString = configuration.GetConnectionString(options.ConnectionName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return ValidateOptionsResult.Fail(
                $"ConnectionStrings:{options.ConnectionName} is required when Redis caching is enabled.");
        }

        try
        {
            _ = ConfigurationOptions.Parse(connectionString);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            return ValidateOptionsResult.Fail(
                $"ConnectionStrings:{options.ConnectionName} must be a valid Redis connection string.");
        }

        return ValidateOptionsResult.Success;
    }

    private static bool IsValidConnectionName(string? connectionName)
    {
        return !string.IsNullOrWhiteSpace(connectionName) &&
            connectionName.Length <= RedisCachingOptions.ConnectionNameMaxLength &&
            connectionName.All(character => !char.IsWhiteSpace(character) && !char.IsControl(character));
    }

    private static bool IsValidInstanceName(string? instanceName)
    {
        return instanceName is not null &&
            instanceName.Length <= RedisCachingOptions.InstanceNameMaxLength &&
            instanceName.All(character => !char.IsWhiteSpace(character) && !char.IsControl(character));
    }
}
