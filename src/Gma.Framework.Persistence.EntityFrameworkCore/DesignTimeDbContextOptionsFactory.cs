namespace Gma.Framework.Persistence.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore;

public static class DesignTimeDbContextOptionsFactory
{
    private const string DefaultPostgreSqlConnection =
        "Host=localhost;Port=5432;Database=generic_modular_api_design;Username=postgres;Password=postgres";

    private const string DefaultSqlServerConnection =
        "Server=localhost,1433;Database=GenericModularApiDesign;User Id=sa;Password=Pass@word1;TrustServerCertificate=True";

    public static DbContextOptions<TDbContext> CreateSqlServerOptions<TDbContext>(
        IReadOnlyList<string> args,
        string migrationsAssembly,
        string migrationsHistorySchema,
        string migrationsHistoryTable)
        where TDbContext : DbContext
    {
        string connectionString = GetConnectionString(args, DatabaseProvider.SqlServer);
        DbContextOptionsBuilder<TDbContext> options = new();

        options.UseSqlServer(
            connectionString,
            builder => builder
                .MigrationsAssembly(migrationsAssembly)
                .MigrationsHistoryTable(migrationsHistoryTable, migrationsHistorySchema));

        return options.Options;
    }

    public static DbContextOptions<TDbContext> CreatePostgreSqlOptions<TDbContext>(
        IReadOnlyList<string> args,
        string migrationsAssembly,
        string migrationsHistorySchema,
        string migrationsHistoryTable)
        where TDbContext : DbContext
    {
        string connectionString = GetConnectionString(args, DatabaseProvider.PostgreSql);
        DbContextOptionsBuilder<TDbContext> options = new();

        options.UseNpgsql(
            connectionString,
            builder => builder
                .MigrationsAssembly(migrationsAssembly)
                .MigrationsHistoryTable(migrationsHistoryTable, migrationsHistorySchema));

        return options.Options;
    }

    private static string GetConnectionString(IReadOnlyList<string> args, DatabaseProvider provider)
    {
        string? value = GetOption(args, "--connection");

        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return provider == DatabaseProvider.PostgreSql
            ? DefaultPostgreSqlConnection
            : DefaultSqlServerConnection;
    }

    private static string? GetOption(IReadOnlyList<string> args, string name)
    {
        for (int index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }
}
