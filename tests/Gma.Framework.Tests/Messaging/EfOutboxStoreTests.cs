namespace Gma.Framework.Tests;

using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public sealed class EfOutboxStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_normalizes_module_name_through_integration_contracts()
    {
        using TestDbContext dbContext = CreateDbContext();

        TestOutboxStore store = new(dbContext, " Auth ");

        Assert.Equal("auth", store.ModuleName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("auth.module")]
    [InlineData("Auth Module")]
    public void Constructor_rejects_invalid_module_name(string moduleName)
    {
        using TestDbContext dbContext = CreateDbContext();

        Assert.Throws<ArgumentException>(() => new TestOutboxStore(dbContext, moduleName));
    }

    [Fact]
    public async Task Claim_pending_claims_only_due_unprocessed_messages()
    {
        using TestDbContext dbContext = CreateDbContext();
        TestOutboxStore store = new(dbContext, "auth", new OutboxOptions { MaxAttempts = 1 });
        Guid dueId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        OutboxMessage due = CreateMessage(dueId, Now);
        OutboxMessage locked = CreateMessage(Guid.Parse("22222222-2222-2222-2222-222222222222"), Now.AddSeconds(1));
        locked.MarkClaimed("worker-old", Now, TimeSpan.FromSeconds(10));
        OutboxMessage retryNotDue = CreateMessage(Guid.Parse("33333333-3333-3333-3333-333333333333"), Now.AddSeconds(2));
        retryNotDue.MarkClaimed("worker-old", Now, TimeSpan.FromSeconds(1));
        retryNotDue.MarkFailed("temporary", Now, maxAttempts: 3);
        OutboxMessage processed = CreateMessage(Guid.Parse("44444444-4444-4444-4444-444444444444"), Now.AddSeconds(3));
        processed.MarkClaimed("worker-old", Now, TimeSpan.FromSeconds(1));
        processed.MarkProcessed(Now);
        OutboxMessage exhausted = CreateMessage(Guid.Parse("55555555-5555-5555-5555-555555555555"), Now.AddSeconds(4));
        exhausted.MarkClaimed("worker-old", Now, TimeSpan.FromSeconds(1));
        exhausted.MarkFailed("temporary", Now, maxAttempts: 1);
        dbContext.OutboxMessages.AddRange(due, locked, retryNotDue, processed, exhausted);
        await dbContext.SaveChangesAsync();

        IReadOnlyList<OutboxMessageRecord> claimed = await store.ClaimPendingAsync(
            10,
            "worker-a",
            Now.AddSeconds(1),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        OutboxMessage dueSnapshot = await dbContext.OutboxMessages.SingleAsync(message => message.Id == dueId);
        Assert.Equal(dueId, Assert.Single(claimed).Id);
        Assert.Equal("worker-a", dueSnapshot.LockedBy);
        Assert.Equal(Now.AddSeconds(31), dueSnapshot.LockedUntilUtc);
    }

    [Fact]
    public async Task Claim_admission_filter_can_suppress_module_owned_scopes()
    {
        using TestDbContext dbContext = CreateDbContext();
        FilteringOutboxStore store = new(dbContext, "tenant-closed");
        OutboxMessage open = CreateMessage(
            Guid.Parse("61111111-1111-1111-1111-111111111111"),
            Now,
            "tenant-open");
        OutboxMessage closed = CreateMessage(
            Guid.Parse("62222222-2222-2222-2222-222222222222"),
            Now,
            "tenant-closed");
        dbContext.OutboxMessages.AddRange(open, closed);
        await dbContext.SaveChangesAsync();

        IReadOnlyList<OutboxMessageRecord> claimed = await store
            .ClaimPendingAsync(
                10,
                "worker-a",
                Now,
                TimeSpan.FromSeconds(30),
                CancellationToken.None);

        Assert.Equal(open.Id, Assert.Single(claimed).Id);
        Assert.Null(closed.LockedBy);
    }

    [Fact]
    public async Task Cleanup_can_be_specialized_by_a_module_owned_store()
    {
        using TestDbContext dbContext = CreateDbContext();
        CleanupOutboxStore store = new(dbContext);
        IOutboxCleanupStore cleanup = store;

        int removed = await cleanup.DeleteProcessedBeforeAsync(
            Now,
            10,
            CancellationToken.None);

        Assert.Equal(37, removed);
        Assert.True(store.WasInvoked);
    }

    [Fact]
    public async Task Cleanup_reports_oldest_retained_processed_message()
    {
        using TestDbContext dbContext = CreateDbContext();
        TestOutboxStore store = new(dbContext, "auth");
        OutboxMessage oldest = CreateMessage(Guid.NewGuid(), Now.AddMinutes(-10));
        oldest.MarkClaimed("worker-a", Now.AddMinutes(-9), TimeSpan.FromMinutes(1));
        oldest.MarkProcessed(Now.AddMinutes(-8));
        OutboxMessage newest = CreateMessage(Guid.NewGuid(), Now.AddMinutes(-5));
        newest.MarkClaimed("worker-a", Now.AddMinutes(-4), TimeSpan.FromMinutes(1));
        newest.MarkProcessed(Now.AddMinutes(-3));
        dbContext.OutboxMessages.AddRange(oldest, newest);
        await dbContext.SaveChangesAsync();

        DateTimeOffset? observed = await store.GetOldestProcessedAtUtcAsync(CancellationToken.None);

        Assert.Equal(Now.AddMinutes(-8), observed);
    }

    [Fact]
    public async Task Mark_processed_is_noop_for_wrong_worker_and_processes_for_owner()
    {
        using TestDbContext dbContext = CreateDbContext();
        TestOutboxStore store = new(dbContext, "auth");
        Guid messageId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        dbContext.OutboxMessages.Add(CreateMessage(messageId, Now));
        await dbContext.SaveChangesAsync();
        await store.ClaimPendingAsync(1, "worker-a", Now, TimeSpan.FromSeconds(30), CancellationToken.None);

        await store.MarkProcessedAsync(messageId, "worker-b", Now.AddSeconds(1), CancellationToken.None);

        OutboxMessage afterWrongWorker = await dbContext.OutboxMessages.SingleAsync(message => message.Id == messageId);
        Assert.Null(afterWrongWorker.ProcessedAtUtc);

        await store.MarkProcessedAsync(messageId, "worker-a", Now.AddSeconds(2), CancellationToken.None);

        OutboxMessage afterOwner = await dbContext.OutboxMessages.SingleAsync(message => message.Id == messageId);
        Assert.Equal(Now.AddSeconds(2), afterOwner.ProcessedAtUtc);
        Assert.Null(afterOwner.LockedBy);
    }

    [Fact]
    public async Task Mark_failed_is_noop_for_wrong_worker_and_records_retry_for_owner()
    {
        using TestDbContext dbContext = CreateDbContext();
        TestOutboxStore store = new(dbContext, "auth");
        Guid messageId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
        dbContext.OutboxMessages.Add(CreateMessage(messageId, Now));
        await dbContext.SaveChangesAsync();
        await store.ClaimPendingAsync(1, "worker-a", Now, TimeSpan.FromSeconds(30), CancellationToken.None);

        await store.MarkFailedAsync(messageId, "worker-b", "wrong worker", Now.AddSeconds(1), CancellationToken.None);

        OutboxMessage afterWrongWorker = await dbContext.OutboxMessages.SingleAsync(message => message.Id == messageId);
        Assert.Equal(0, afterWrongWorker.Attempts);
        Assert.Null(afterWrongWorker.Error);

        await store.MarkFailedAsync(messageId, "worker-a", "temporary", Now.AddSeconds(2), CancellationToken.None);

        OutboxMessage afterOwner = await dbContext.OutboxMessages.SingleAsync(message => message.Id == messageId);
        Assert.Equal(1, afterOwner.Attempts);
        Assert.Equal("temporary", afterOwner.Error);
        Assert.Null(afterOwner.LockedBy);
        Assert.Equal(Now.AddSeconds(4), afterOwner.NextAttemptAtUtc);
    }

    [Fact]
    public async Task Backlog_snapshot_separates_pending_and_exhausted_messages()
    {
        using TestDbContext dbContext = CreateDbContext();
        TestOutboxStore store = new(dbContext, "auth", new OutboxOptions { MaxAttempts = 1 });
        OutboxMessage pending = CreateMessage(Guid.Parse("10000000-0000-0000-0000-000000000001"), Now.AddMinutes(-5));
        OutboxMessage exhausted = CreateMessage(Guid.Parse("10000000-0000-0000-0000-000000000002"), Now.AddMinutes(-4));
        exhausted.MarkClaimed("worker-a", Now.AddMinutes(-3), TimeSpan.FromSeconds(1));
        exhausted.MarkFailed("permanent", Now.AddMinutes(-3), maxAttempts: 1);
        OutboxMessage processed = CreateMessage(Guid.Parse("10000000-0000-0000-0000-000000000003"), Now.AddMinutes(-6));
        processed.MarkClaimed("worker-a", Now.AddMinutes(-2), TimeSpan.FromSeconds(1));
        processed.MarkProcessed(Now.AddMinutes(-2));
        dbContext.OutboxMessages.AddRange(pending, exhausted, processed);
        await dbContext.SaveChangesAsync();

        OutboxBacklogSnapshot snapshot = await store.GetBacklogAsync(Now, CancellationToken.None);

        Assert.Equal("auth", snapshot.ModuleName);
        Assert.Equal(1, snapshot.PendingCount);
        Assert.Equal(1, snapshot.ExhaustedCount);
        Assert.Equal(Now.AddMinutes(-5), snapshot.OldestPendingAtUtc);
        Assert.Equal(TimeSpan.FromMinutes(5), snapshot.OldestPendingAge);
    }

    private static TestDbContext CreateDbContext()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new TestDbContext(options);
    }

    private static OutboxMessage CreateMessage(
        Guid id,
        DateTimeOffset createdAtUtc,
        string scopeId = "tenant-a") =>
        new(
            id,
            "gma.auth.test.v1",
            "test",
            1,
            scopeId,
            createdAtUtc,
            "{}",
            createdAtUtc);

    private sealed class TestOutboxStore(
        TestDbContext dbContext,
        string moduleName,
        OutboxOptions? options = null)
        : EfOutboxStore<TestDbContext>(dbContext, Options.Create(options ?? new OutboxOptions()), moduleName);

    private sealed class FilteringOutboxStore(
        TestDbContext dbContext,
        string excludedScope)
        : EfOutboxStore<TestDbContext>(
            dbContext,
            Options.Create(new OutboxOptions()),
            "auth")
    {
        protected override IQueryable<OutboxMessage> ApplyClaimAdmission(
            IQueryable<OutboxMessage> candidates) =>
            candidates.Where(message => message.ScopeId != excludedScope);
    }

    private sealed class CleanupOutboxStore(TestDbContext dbContext)
        : EfOutboxStore<TestDbContext>(
            dbContext,
            Options.Create(new OutboxOptions()),
            "auth")
    {
        public bool WasInvoked { get; private set; }

        public override Task<int> DeleteProcessedBeforeAsync(
            DateTimeOffset processedBeforeUtc,
            int maxMessages,
            CancellationToken cancellationToken)
        {
            this.WasInvoked = true;
            return Task.FromResult(37);
        }
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<OutboxMessage> OutboxMessages => this.Set<OutboxMessage>();
    }
}
