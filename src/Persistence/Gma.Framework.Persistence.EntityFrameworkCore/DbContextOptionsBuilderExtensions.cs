namespace Gma.Framework.Persistence.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

public static class DbContextOptionsBuilderExtensions
{
    public static DbContextOptionsBuilder UseConfiguredProvider(
        this DbContextOptionsBuilder options,
        IConfiguration configuration,
        string sqlServerMigrationsAssembly,
        string postgreSqlMigrationsAssembly,
        string? migrationsHistorySchema = null,
        string migrationsHistoryTable = "__ef_migrations_history")
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(sqlServerMigrationsAssembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(postgreSqlMigrationsAssembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(migrationsHistoryTable);

        PersistenceOptions persistenceOptions = PersistenceOptionsValidation.GetValidatedOptions(configuration);

        return persistenceOptions.Provider switch
        {
            DatabaseProvider.PostgreSql => options.UseNpgsql(
                GetRequiredConnectionString(configuration, "PostgreSql"),
                provider =>
                {
                    provider.MigrationsAssembly(postgreSqlMigrationsAssembly);
                    provider.MigrationsHistoryTable(migrationsHistoryTable, migrationsHistorySchema);
                }),
            DatabaseProvider.SqlServer => options.UseSqlServer(
                GetRequiredConnectionString(configuration, "SqlServer"),
                provider =>
                {
                    provider.MigrationsAssembly(sqlServerMigrationsAssembly);
                    provider.MigrationsHistoryTable(migrationsHistoryTable, migrationsHistorySchema);
                }),
            _ => throw new InvalidOperationException(
                $"Unsupported persistence provider '{persistenceOptions.Provider}'.")
        };
    }

    private static string GetRequiredConnectionString(IConfiguration configuration, string name)
    {
        string? connectionString = configuration.GetConnectionString(name);

        return string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException($"Connection string '{name}' is required for the configured persistence provider.")
            : connectionString;
    }
}
