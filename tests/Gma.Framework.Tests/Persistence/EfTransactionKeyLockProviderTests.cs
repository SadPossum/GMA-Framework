namespace Gma.Framework.Tests.Persistence;

using System.Diagnostics;
using Gma.Framework.Cqrs;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Xunit;

[Trait("Category", "Unit")]
[Trait("Category", "Integration")]
[Trait("Category", "Docker")]
public sealed class EfTransactionKeyLockProviderTests
{
    [DockerFact]
    public async Task PostgreSql_preserves_the_transaction_coordination_failure_contract()
    {
        await using PostgreSqlContainer postgreSql =
            new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgreSql.StartAsync();

        await VerifyFailureContractAsync(() => new TestDbContext(
            new DbContextOptionsBuilder<TestDbContext>()
                .UseNpgsql(postgreSql.GetConnectionString())
                .Options));
    }

    [DockerFact]
    public async Task SqlServer_preserves_the_transaction_coordination_failure_contract()
    {
        await using MsSqlContainer sqlServer = new MsSqlBuilder(
                "mcr.microsoft.com/mssql/server:2022-latest")
            .Build();
        await sqlServer.StartAsync();

        await VerifyFailureContractAsync(() => new TestDbContext(
            new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlServer(sqlServer.GetConnectionString())
                .Options));
    }

    private static async Task VerifyFailureContractAsync(Func<TestDbContext> createDbContext)
    {
        string resource = $"framework-test:{Guid.NewGuid():N}";
        await using TestDbContext holder = createDbContext();
        await using var holderTransaction = await holder.Database.BeginTransactionAsync();
        await EfTransactionKeyLock.AcquireAsync(
            holder,
            resource,
            TimeSpan.FromSeconds(5));

        await VerifyTimeoutAsync(createDbContext, resource);
        await VerifyCallerCancellationAsync(createDbContext, resource);

        await holderTransaction.RollbackAsync();

        await using TestDbContext reacquirer = createDbContext();
        await using var reacquirerTransaction = await reacquirer.Database.BeginTransactionAsync();
        await EfTransactionKeyLock.AcquireAsync(
            reacquirer,
            resource,
            TimeSpan.FromSeconds(2));
        await reacquirerTransaction.RollbackAsync();
    }

    private static async Task VerifyTimeoutAsync(
        Func<TestDbContext> createDbContext,
        string resource)
    {
        await using TestDbContext contender = createDbContext();
        await using var transaction = await contender.Database.BeginTransactionAsync();
        long startedAt = Stopwatch.GetTimestamp();

        TransactionCoordinationException exception =
            await Assert.ThrowsAsync<TransactionCoordinationException>(() =>
                EfTransactionKeyLock.AcquireAsync(
                    contender,
                    resource,
                    TimeSpan.FromMilliseconds(250)));

        Assert.Equal(TransactionCoordinationFailure.TimedOut, exception.Failure);
        Assert.True(
            Stopwatch.GetElapsedTime(startedAt) < TimeSpan.FromSeconds(5),
            "The provider did not honor the bounded coordination timeout.");
        await transaction.RollbackAsync();
    }

    private static async Task VerifyCallerCancellationAsync(
        Func<TestDbContext> createDbContext,
        string resource)
    {
        await using TestDbContext contender = createDbContext();
        await using var transaction = await contender.Database.BeginTransactionAsync();
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(250));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            EfTransactionKeyLock.AcquireAsync(
                contender,
                resource,
                TimeSpan.FromSeconds(5),
                cancellation.Token));

        await transaction.RollbackAsync();
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options)
        : DbContext(options);
}
