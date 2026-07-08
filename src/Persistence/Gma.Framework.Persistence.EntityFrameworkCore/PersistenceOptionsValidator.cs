namespace Gma.Framework.Persistence.EntityFrameworkCore;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

internal sealed class PersistenceOptionsValidator(IConfiguration configuration) : IValidateOptions<PersistenceOptions>
{
    public ValidateOptionsResult Validate(string? name, PersistenceOptions options)
    {
        if (!Enum.IsDefined(options.Provider) || options.Provider == DatabaseProvider.Unknown)
        {
            return ValidateOptionsResult.Fail($"{PersistenceOptions.SectionName}:Provider is not supported.");
        }

        string connectionName = options.Provider switch
        {
            DatabaseProvider.PostgreSql => "PostgreSql",
            DatabaseProvider.SqlServer => "SqlServer",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString(connectionName)))
        {
            return ValidateOptionsResult.Fail($"ConnectionStrings:{connectionName} is required for {options.Provider}.");
        }

        return ValidateOptionsResult.Success;
    }
}
