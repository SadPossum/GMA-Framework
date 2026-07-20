namespace Gma.Framework.Tests;

using Gma.Framework.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

[Trait("Category", "Unit")]
public sealed class EfDatabaseExceptionClassifierTests
{
    [Theory]
    [InlineData(2601, null, true)]
    [InlineData(2627, null, true)]
    [InlineData(547, null, false)]
    [InlineData(null, PostgresErrorCodes.UniqueViolation, true)]
    [InlineData(null, PostgresErrorCodes.ForeignKeyViolation, false)]
    [InlineData(null, null, false)]
    public void Provider_codes_are_classified(
        int? sqlServerErrorNumber,
        string? postgreSqlState,
        bool expected) =>
        Assert.Equal(
            expected,
            EfDatabaseExceptionClassifier.IsUniqueConstraintViolation(
                sqlServerErrorNumber,
                postgreSqlState));

    [Fact]
    public void Wrapped_postgresql_unique_violation_is_classified()
    {
        PostgresException providerException = new(
            "duplicate key",
            "ERROR",
            "ERROR",
            PostgresErrorCodes.UniqueViolation);
        DbUpdateException exception = new("Update failed.", providerException);

        Assert.True(EfDatabaseExceptionClassifier.IsUniqueConstraintViolation(exception));
    }

    [Fact]
    public void Null_exception_is_rejected() =>
        Assert.Throws<ArgumentNullException>(() =>
            EfDatabaseExceptionClassifier.IsUniqueConstraintViolation(null!));
}
