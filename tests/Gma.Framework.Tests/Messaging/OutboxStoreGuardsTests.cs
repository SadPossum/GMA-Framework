namespace Gma.Framework.Tests;

using Gma.Framework.Messaging.Infrastructure;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OutboxStoreGuardsTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Validate_claim_arguments_normalizes_worker_id_and_preserves_values()
    {
        var result = OutboxStoreGuards.ValidateClaimArguments(
            25,
            " worker-a ",
            Now,
            TimeSpan.FromSeconds(30));

        Assert.Equal(25, result.BatchSize);
        Assert.Equal("worker-a", result.WorkerId);
        Assert.Equal(Now, result.NowUtc);
        Assert.Equal(TimeSpan.FromSeconds(30), result.LockDuration);
    }

    [Fact]
    public void Validate_claim_arguments_rejects_invalid_values()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OutboxStoreGuards.ValidateClaimArguments(0, "worker-a", Now, TimeSpan.FromSeconds(30)));
        Assert.Throws<ArgumentException>(() =>
            OutboxStoreGuards.ValidateClaimArguments(25, "worker a", Now, TimeSpan.FromSeconds(30)));
        Assert.Throws<ArgumentException>(() =>
            OutboxStoreGuards.ValidateClaimArguments(25, "worker-a", default, TimeSpan.FromSeconds(30)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OutboxStoreGuards.ValidateClaimArguments(25, "worker-a", Now, TimeSpan.Zero));
    }

    [Fact]
    public void Validate_mark_arguments_normalizes_worker_id_and_preserves_values()
    {
        Guid id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        var result = OutboxStoreGuards.ValidateMarkArguments(id, " worker-a ", Now);

        Assert.Equal(id, result.Id);
        Assert.Equal("worker-a", result.WorkerId);
        Assert.Equal(Now, result.NowUtc);
    }

    [Fact]
    public void Validate_mark_arguments_rejects_invalid_values()
    {
        Guid id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        Assert.Throws<ArgumentException>(() =>
            OutboxStoreGuards.ValidateMarkArguments(Guid.Empty, "worker-a", Now));
        Assert.Throws<ArgumentException>(() =>
            OutboxStoreGuards.ValidateMarkArguments(id, "worker a", Now));
        Assert.Throws<ArgumentException>(() =>
            OutboxStoreGuards.ValidateMarkArguments(id, "worker-a", default));
    }
}
