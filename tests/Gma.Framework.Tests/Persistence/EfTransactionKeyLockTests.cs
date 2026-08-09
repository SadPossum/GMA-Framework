namespace Gma.Framework.Tests.Persistence;

using Gma.Framework.Cqrs;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class EfTransactionKeyLockTests
{
    [Fact]
    public async Task Acquire_requires_an_active_transaction()
    {
        await using TestDbContext dbContext = CreateDbContext();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            EfTransactionKeyLock.AcquireAsync(dbContext, "resource"));

        Assert.Contains("active database transaction", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Acquire_rejects_invalid_resources(string resource)
    {
        await using TestDbContext dbContext = CreateDbContext();

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            EfTransactionKeyLock.AcquireAsync(dbContext, resource));
    }

    [Fact]
    public async Task Acquire_rejects_an_overlong_resource()
    {
        await using TestDbContext dbContext = CreateDbContext();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            EfTransactionKeyLock.AcquireAsync(dbContext, new string('x', 1_025)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public async Task Acquire_rejects_an_invalid_mode(int mode)
    {
        await using TestDbContext dbContext = CreateDbContext();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            EfTransactionKeyLock.AcquireAsync(
                dbContext,
                "resource",
                (EfTransactionKeyLockMode)mode));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(301)]
    public async Task Acquire_rejects_invalid_timeouts(int seconds)
    {
        await using TestDbContext dbContext = CreateDbContext();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            EfTransactionKeyLock.AcquireAsync(dbContext, "resource", TimeSpan.FromSeconds(seconds)));
    }

    [Theory]
    [InlineData(-1, TransactionCoordinationFailure.TimedOut)]
    [InlineData(-2, TransactionCoordinationFailure.CanceledByProvider)]
    [InlineData(-3, TransactionCoordinationFailure.DeadlockVictim)]
    public void Sql_server_transient_results_have_a_stable_failure_contract(
        int resultCode,
        TransactionCoordinationFailure expectedFailure)
    {
        TransactionCoordinationException exception = Assert.IsType<TransactionCoordinationException>(
            EfTransactionKeyLock.CreateSqlServerFailure(resultCode));

        Assert.Equal(expectedFailure, exception.Failure);
        Assert.DoesNotContain(resultCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
            exception.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-999)]
    [InlineData(-4)]
    [InlineData(int.MinValue)]
    public void Sql_server_non_transient_results_remain_unexpected_failures(int resultCode)
    {
        Exception exception = EfTransactionKeyLock.CreateSqlServerFailure(resultCode);

        Assert.IsType<InvalidOperationException>(exception);
        Assert.IsNotType<TransactionCoordinationException>(exception);
        Assert.DoesNotContain(resultCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
            exception.Message,
            StringComparison.Ordinal);
    }

    private static TestDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options);
}
