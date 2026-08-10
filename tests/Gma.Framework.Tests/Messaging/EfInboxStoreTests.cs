namespace Gma.Framework.Tests;

using System.Data;
using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

[Trait("Category", "Unit")]
public sealed class EfInboxStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Processing_transactions_use_read_committed_isolation()
    {
        Assert.Equal(
            IsolationLevel.ReadCommitted,
            EfInboxStore<TestDbContext>.TransactionIsolationLevel);
    }

    [Fact]
    public void Constructor_normalizes_module_name_through_integration_contracts()
    {
        using TestDbContext dbContext = CreateDbContext();

        TestInboxStore store = new(dbContext, " Ordering ");

        Assert.Equal("ordering", store.ModuleName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("ordering.module")]
    [InlineData("Ordering Module")]
    public void Constructor_rejects_invalid_module_name(string moduleName)
    {
        using TestDbContext dbContext = CreateDbContext();

        Assert.Throws<ArgumentException>(() => new TestInboxStore(dbContext, moduleName));
    }

    [Fact]
    public async Task Process_async_records_processed_message_and_invokes_handler_once()
    {
        using TestDbContext dbContext = CreateDbContext();
        TestInboxStore store = new(dbContext, "ordering");
        InboxMessageRecord message = CreateMessageRecord();
        int calls = 0;
        CancellationToken observedToken = default;
        using CancellationTokenSource cancellation = new();

        InboxProcessResult result = await store.ProcessAsync(
            message,
            token =>
            {
                calls++;
                observedToken = token;
                return Task.CompletedTask;
            },
            cancellation.Token);

        InboxMessage inboxMessage = await dbContext.InboxMessages.SingleAsync();
        Assert.Equal(InboxProcessStatus.Processed, result.Status);
        Assert.Equal(InboxMessageStatus.Processed, inboxMessage.Status);
        Assert.Equal(1, calls);
        Assert.Equal(cancellation.Token, observedToken);
        Assert.Equal(Now, inboxMessage.ProcessedAtUtc);
    }

    [Fact]
    public async Task Process_async_returns_duplicate_without_invoking_handler_for_processed_message()
    {
        using TestDbContext dbContext = CreateDbContext();
        TestInboxStore store = new(dbContext, "ordering");
        InboxMessageRecord message = CreateMessageRecord();
        await store.ProcessAsync(message, _ => Task.CompletedTask, CancellationToken.None);

        InboxProcessResult duplicate = await store.ProcessAsync(
            message,
            _ => throw new InvalidOperationException("Handler should not run."),
            CancellationToken.None);

        Assert.Equal(InboxProcessStatus.Duplicate, duplicate.Status);
    }

    [Fact]
    public async Task Process_async_suppresses_before_inbox_insert_or_handler()
    {
        using TestDbContext dbContext = CreateDbContext();
        TestInboxStore store = new(dbContext, "ordering")
        {
            Admission = (_, _) => ValueTask.FromResult(false)
        };
        int calls = 0;

        InboxProcessResult result = await store.ProcessAsync(
            CreateMessageRecord(),
            _ =>
            {
                calls++;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.Equal(InboxProcessStatus.Suppressed, result.Status);
        Assert.Null(result.Error);
        Assert.Equal(0, calls);
        Assert.Empty(dbContext.InboxMessages);
    }

    [Fact]
    public async Task Process_async_checks_processed_duplicate_before_admission()
    {
        using TestDbContext dbContext = CreateDbContext();
        InboxMessageRecord message = CreateMessageRecord();
        await new TestInboxStore(dbContext, "ordering").ProcessAsync(
            message,
            _ => Task.CompletedTask,
            CancellationToken.None);
        TestInboxStore denyingStore = new(dbContext, "ordering")
        {
            Admission = (_, _) => throw new InvalidOperationException(
                "Admission should not run for a processed duplicate.")
        };

        InboxProcessResult result = await denyingStore.ProcessAsync(
            message,
            _ => throw new InvalidOperationException("Handler should not run."),
            CancellationToken.None);

        Assert.Equal(InboxProcessStatus.Duplicate, result.Status);
    }

    [Fact]
    public async Task Process_async_suppresses_failure_evidence_when_admission_closes()
    {
        using TestDbContext dbContext = CreateDbContext();
        int admissionCalls = 0;
        TestInboxStore store = new(dbContext, "ordering")
        {
            Admission = (_, _) =>
                ValueTask.FromResult(Interlocked.Increment(ref admissionCalls) == 1)
        };

        InboxProcessResult result = await store.ProcessAsync(
            CreateMessageRecord(),
            _ => throw new InvalidOperationException("handler failed"),
            CancellationToken.None);

        Assert.Equal(InboxProcessStatus.Suppressed, result.Status);
        Assert.Equal(2, admissionCalls);
        Assert.Empty(dbContext.InboxMessages);
    }

    [Fact]
    public async Task Process_async_records_handler_failure()
    {
        const string personalDataCanary = "guest.aprokudanov@example.test";
        using TestDbContext dbContext = CreateDbContext();
        TestInboxStore store = new(dbContext, "ordering");
        InboxMessageRecord message = CreateMessageRecord();

        InboxProcessResult result = await store.ProcessAsync(
            message,
            _ => throw new InvalidOperationException(personalDataCanary),
            CancellationToken.None);

        InboxMessage inboxMessage = await dbContext.InboxMessages.SingleAsync();
        Assert.Equal(InboxProcessStatus.Failed, result.Status);
        Assert.Equal("inbox-handler-failed:InvalidOperationException", result.Error);
        Assert.DoesNotContain(personalDataCanary, result.Error, StringComparison.Ordinal);
        Assert.Equal(InboxMessageStatus.Failed, inboxMessage.Status);
        Assert.Equal("inbox-handler-failed:InvalidOperationException", inboxMessage.LastError);
        Assert.DoesNotContain(personalDataCanary, inboxMessage.LastError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Process_async_records_null_handler_task_as_explicit_failure()
    {
        using TestDbContext dbContext = CreateDbContext();
        TestInboxStore store = new(dbContext, "ordering");
        InboxMessageRecord message = CreateMessageRecord();

        InboxProcessResult result = await store.ProcessAsync(
            message,
            _ => null!,
            CancellationToken.None);

        InboxMessage inboxMessage = await dbContext.InboxMessages.SingleAsync();
        Assert.Equal(InboxProcessStatus.Failed, result.Status);
        Assert.Equal("inbox-handler-failed:InvalidOperationException", result.Error);
        Assert.Equal(InboxMessageStatus.Failed, inboxMessage.Status);
        Assert.Equal("inbox-handler-failed:InvalidOperationException", inboxMessage.LastError);
    }

    [Fact]
    public async Task Process_async_records_handler_cancellation_when_host_is_not_stopping()
    {
        using TestDbContext dbContext = CreateDbContext();
        TestInboxStore store = new(dbContext, "ordering");
        InboxMessageRecord message = CreateMessageRecord();

        InboxProcessResult result = await store.ProcessAsync(
            message,
            _ => throw new OperationCanceledException(),
            CancellationToken.None);

        InboxMessage inboxMessage = await dbContext.InboxMessages.SingleAsync();
        Assert.Equal(InboxProcessStatus.Failed, result.Status);
        Assert.Equal("Handler execution was canceled before completion.", result.Error);
        Assert.Equal(InboxMessageStatus.Failed, inboxMessage.Status);
        Assert.Equal("Handler execution was canceled before completion.", inboxMessage.LastError);
    }

    private static TestDbContext CreateDbContext()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new TestDbContext(options);
    }

    private static InboxMessageRecord CreateMessageRecord() =>
        new(
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            "catalog-item-created-projection",
            "gma.catalog.item-created.v1",
            "item-created",
            1,
            "tenant-a",
            Now);

    private sealed class TestInboxStore(
        TestDbContext dbContext,
        string moduleName)
        : EfInboxStore<TestDbContext>(
            dbContext,
            new TestClock(),
            new TestIdGenerator(),
            moduleName)
    {
        public Func<
            InboxMessageRecord,
            CancellationToken,
            ValueTask<bool>> Admission
        { get; init; } =
            (_, _) => ValueTask.FromResult(true);

        protected override ValueTask<bool> IsAdmittedAsync(
            InboxMessageRecord message,
            CancellationToken cancellationToken) =>
            this.Admission(message, cancellationToken);
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<InboxMessage> InboxMessages => this.Set<InboxMessage>();
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.Parse("11111111-2222-3333-4444-555555555555");
    }
}
