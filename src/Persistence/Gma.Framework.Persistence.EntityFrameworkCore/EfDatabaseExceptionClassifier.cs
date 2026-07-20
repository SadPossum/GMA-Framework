namespace Gma.Framework.Persistence.EntityFrameworkCore;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;

public static class EfDatabaseExceptionClassifier
{
    public static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres &&
                IsUniqueConstraintViolation(sqlServerErrorNumber: null, postgres.SqlState))
            {
                return true;
            }

            if (current is SqlException sqlServer &&
                IsUniqueConstraintViolation(sqlServer.Number, postgreSqlState: null))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsUniqueConstraintViolation(
        int? sqlServerErrorNumber,
        string? postgreSqlState) =>
        sqlServerErrorNumber is 2601 or 2627 ||
        string.Equals(postgreSqlState, PostgresErrorCodes.UniqueViolation, StringComparison.Ordinal);
}
