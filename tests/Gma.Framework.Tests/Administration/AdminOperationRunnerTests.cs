namespace Gma.Framework.Tests;

using Gma.Framework.Naming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Gma.Framework.Administration;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Tenancy;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdminOperationRunnerTests
{
    [Fact]
    public async Task Pre_authorization_failure_is_audited_before_authorization_and_action()
    {
        RecordingAuthorizationService authorization = new();
        RecordingAuditSink audit = new();
        FixedClock clock = new(new DateTimeOffset(2026, 7, 1, 12, 30, 0, TimeSpan.Zero));
        FixedIdGenerator idGenerator = new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        ServiceProvider services = new ServiceCollection()
            .AddLogging()
            .AddScoped<ITenantContext>(_ => new EnabledTenantContext())
            .AddScoped<ITenantContextAccessor>(_ => new EnabledTenantContext())
            .AddSingleton<ISystemClock>(clock)
            .AddSingleton<IIdGenerator>(idGenerator)
            .AddScoped<IAdminAuthorizationService>(_ => authorization)
            .AddScoped<IAdminAuditSink>(_ => audit)
            .AddGmaAdministration()
            .BuildServiceProvider();

        IAdminOperationRunner runner = services.GetRequiredService<IAdminOperationRunner>();
        int actionExecutions = 0;

        AdminOperationExecutionResult<int> result = await runner.ExecuteAsync<int>(
            new AdminOperationContext(
                AdminActor.System("actor"),
                AdminOperation.Create("auth.members.create", AdminPermission.Create("auth.members.create")),
                "tenant-a",
                RequireTenant: true,
                PreAuthorizationError: AdminErrors.TenantClaimMismatch),
            _ =>
            {
                actionExecutions++;
                return Task.FromResult(Result.Success(42));
            },
            CancellationToken.None);

        Assert.Equal(AdminOperationExecutionStatus.ValidationFailed, result.Status);
        Assert.Equal(AdminErrors.TenantClaimMismatch, result.Result.Error);
        Assert.Equal(0, actionExecutions);
        Assert.Equal(0, authorization.CallCount);

        AdminAuditRecord record = Assert.Single(audit.Records);
        Assert.Equal(idGenerator.Id, record.Id);
        Assert.Equal(clock.UtcNow, record.CreatedAtUtc);
        Assert.Equal("tenant-a", record.TenantId);
        Assert.Equal(AdminAuditResult.Denied, record.Result);
        Assert.Equal(AdminAuditResults.Denied, record.ResultName);
        Assert.Equal(AdminErrors.TenantClaimMismatch.Code, record.ErrorCode);
    }

    [Fact]
    public async Task Invalid_tenant_is_audited_before_authorization_and_action()
    {
        RecordingAuthorizationService authorization = new();
        RecordingAuditSink audit = new();
        FixedClock clock = new(new DateTimeOffset(2026, 7, 1, 12, 45, 0, TimeSpan.Zero));
        EnabledTenantContext tenantContext = new() { InitialScopeId = "stale-tenant" };
        ServiceProvider services = CreateServices(authorization, audit, clock, tenantContext: tenantContext);
        IAdminOperationRunner runner = services.GetRequiredService<IAdminOperationRunner>();
        int actionExecutions = 0;

        AdminOperationExecutionResult<int> result = await runner.ExecuteAsync<int>(
            new AdminOperationContext(
                AdminActor.System("actor"),
                AdminOperation.Create("auth.members.create", AdminPermission.Create("auth.members.create")),
                new string('x', ScopeIds.MaxLength + 1),
                RequireTenant: false),
            _ =>
            {
                actionExecutions++;
                return Task.FromResult(Result.Success(42));
            },
            CancellationToken.None);

        Assert.Equal(AdminOperationExecutionStatus.ValidationFailed, result.Status);
        Assert.Equal(AdminErrors.TenantInvalid, result.Result.Error);
        Assert.Equal(0, authorization.CallCount);
        Assert.Equal(0, actionExecutions);

        AdminAuditRecord record = Assert.Single(audit.Records);
        Assert.Equal(AdminAuditResult.Denied, record.Result);
        Assert.Equal(AdminErrors.TenantInvalid.Code, record.ErrorCode);
        Assert.Null(tenantContext.TenantId);
    }

    [Fact]
    public async Task Action_exception_is_logged_audited_and_returned_as_unexpected_failure()
    {
        RecordingAuditSink audit = new();
        FixedClock clock = new(new DateTimeOffset(2026, 7, 1, 13, 0, 0, TimeSpan.Zero));
        ServiceProvider services = CreateServices(new RecordingAuthorizationService(), audit, clock);
        IAdminOperationRunner runner = services.GetRequiredService<IAdminOperationRunner>();

        AdminOperationExecutionResult<int> result = await runner.ExecuteAsync<int>(
            new AdminOperationContext(
                AdminActor.System("actor"),
                AdminOperation.Create("auth.members.create", AdminPermission.Create("auth.members.create")),
                "tenant-a",
                RequireTenant: true),
            _ => throw new InvalidOperationException("database unavailable"),
            CancellationToken.None);

        Assert.Equal(AdminOperationExecutionStatus.UnexpectedFailure, result.Status);
        Assert.Equal(AdminErrors.OperationFailed, result.Result.Error);

        AdminAuditRecord record = Assert.Single(audit.Records);
        Assert.Equal(AdminAuditResult.Failed, record.Result);
        Assert.Equal(AdminErrors.OperationFailed.Code, record.ErrorCode);
    }

    [Fact]
    public async Task Null_action_result_is_logged_audited_and_returned_as_unexpected_failure()
    {
        RecordingAuditSink audit = new();
        FixedClock clock = new(new DateTimeOffset(2026, 7, 1, 13, 15, 0, TimeSpan.Zero));
        ServiceProvider services = CreateServices(new RecordingAuthorizationService(), audit, clock);
        IAdminOperationRunner runner = services.GetRequiredService<IAdminOperationRunner>();

        AdminOperationExecutionResult<int> result = await runner.ExecuteAsync<int>(
            new AdminOperationContext(
                AdminActor.System("actor"),
                AdminOperation.Create("auth.members.create", AdminPermission.Create("auth.members.create")),
                "tenant-a",
                RequireTenant: true),
            _ => Task.FromResult<Result<int>>(null!),
            CancellationToken.None);

        Assert.Equal(AdminOperationExecutionStatus.UnexpectedFailure, result.Status);
        Assert.Equal(AdminErrors.OperationFailed, result.Result.Error);

        AdminAuditRecord record = Assert.Single(audit.Records);
        Assert.Equal(AdminAuditResult.Failed, record.Result);
        Assert.Equal(AdminErrors.OperationFailed.Code, record.ErrorCode);
    }

    [Fact]
    public async Task Authorization_exception_is_logged_audited_and_returned_as_unexpected_failure()
    {
        RecordingAuditSink audit = new();
        FixedClock clock = new(new DateTimeOffset(2026, 7, 1, 13, 30, 0, TimeSpan.Zero));
        ServiceProvider services = CreateServices(new ThrowingAuthorizationService(), audit, clock);
        IAdminOperationRunner runner = services.GetRequiredService<IAdminOperationRunner>();
        int actionExecutions = 0;

        AdminOperationExecutionResult<int> result = await runner.ExecuteAsync(
            new AdminOperationContext(
                AdminActor.System("actor"),
                AdminOperation.Create("auth.members.create", AdminPermission.Create("auth.members.create")),
                "tenant-a",
                RequireTenant: true),
            _ =>
            {
                actionExecutions++;
                return Task.FromResult(Result.Success(42));
            },
            CancellationToken.None);

        Assert.Equal(AdminOperationExecutionStatus.UnexpectedFailure, result.Status);
        Assert.Equal(AdminErrors.OperationFailed, result.Result.Error);
        Assert.Equal(0, actionExecutions);

        AdminAuditRecord record = Assert.Single(audit.Records);
        Assert.Equal(AdminAuditResult.Failed, record.Result);
        Assert.Equal(AdminErrors.OperationFailed.Code, record.ErrorCode);
    }

    [Fact]
    public async Task Action_exception_is_still_shaped_when_logger_throws()
    {
        RecordingAuditSink audit = new();
        ServiceProvider services = CreateServices(
            new RecordingAuthorizationService(),
            audit,
            new FixedClock(new DateTimeOffset(2026, 7, 1, 14, 0, 0, TimeSpan.Zero)),
            addThrowingLogger: true);
        IAdminOperationRunner runner = services.GetRequiredService<IAdminOperationRunner>();

        AdminOperationExecutionResult<int> result = await runner.ExecuteAsync<int>(
            new AdminOperationContext(
                AdminActor.System("actor"),
                AdminOperation.Create("auth.members.create", AdminPermission.Create("auth.members.create")),
                "tenant-a",
                RequireTenant: true),
            _ => throw new InvalidOperationException("database unavailable"),
            CancellationToken.None);

        Assert.Equal(AdminOperationExecutionStatus.UnexpectedFailure, result.Status);
        Assert.Equal(AdminErrors.OperationFailed, result.Result.Error);
        AdminAuditRecord record = Assert.Single(audit.Records);
        Assert.Equal(AdminErrors.OperationFailed.Code, record.ErrorCode);
    }

    [Fact]
    public async Task Audit_failure_is_reported_when_logger_also_throws()
    {
        ServiceProvider services = CreateServices(
            new RecordingAuthorizationService(),
            new ThrowingAuditSink(),
            new FixedClock(new DateTimeOffset(2026, 7, 1, 14, 30, 0, TimeSpan.Zero)),
            addThrowingLogger: true);
        IAdminOperationRunner runner = services.GetRequiredService<IAdminOperationRunner>();

        AdminOperationExecutionResult<int> result = await runner.ExecuteAsync(
            new AdminOperationContext(
                AdminActor.System("actor"),
                AdminOperation.Create("auth.members.create", AdminPermission.Create("auth.members.create")),
                "tenant-a",
                RequireTenant: true),
            _ => Task.FromResult(Result.Success(42)),
            CancellationToken.None);

        Assert.Equal(AdminOperationExecutionStatus.Succeeded, result.Status);
        Assert.Equal(42, result.Result.Value);
        Assert.Equal("Admin audit failed.", result.AuditError);
    }

    [Fact]
    public async Task Missing_audit_sink_is_reported_instead_of_silently_discarding_the_record()
    {
        ServiceProvider services = CreateServices(
            new RecordingAuthorizationService(),
            audit: null,
            new FixedClock(new DateTimeOffset(2026, 7, 1, 14, 45, 0, TimeSpan.Zero)));
        IAdminOperationRunner runner = services.GetRequiredService<IAdminOperationRunner>();

        AdminOperationExecutionResult<int> result = await runner.ExecuteAsync(
            new AdminOperationContext(
                AdminActor.System("actor"),
                AdminOperation.Create("auth.members.create", AdminPermission.Create("auth.members.create")),
                "tenant-a",
                RequireTenant: true),
            _ => Task.FromResult(Result.Success(42)),
            CancellationToken.None);

        Assert.Equal(AdminOperationExecutionStatus.Succeeded, result.Status);
        Assert.Equal("Admin audit failed.", result.AuditError);
    }

    [Fact]
    public async Task Explicit_null_sink_is_a_conscious_successful_opt_out()
    {
        ServiceProvider services = CreateServices(
            new RecordingAuthorizationService(),
            new NullAdminAuditSink(),
            new FixedClock(new DateTimeOffset(2026, 7, 1, 14, 50, 0, TimeSpan.Zero)));
        IAdminOperationRunner runner = services.GetRequiredService<IAdminOperationRunner>();

        AdminOperationExecutionResult<int> result = await runner.ExecuteAsync(
            new AdminOperationContext(
                AdminActor.System("actor"),
                AdminOperation.Create("auth.members.create", AdminPermission.Create("auth.members.create")),
                "tenant-a",
                RequireTenant: true),
            _ => Task.FromResult(Result.Success(42)),
            CancellationToken.None);

        Assert.Null(result.AuditError);
    }

    [Fact]
    public async Task Terminal_audit_write_uses_a_bounded_token_independent_of_caller_cancellation()
    {
        RecordingAuditSink audit = new();
        ServiceProvider services = CreateServices(
            new RecordingAuthorizationService(),
            audit,
            new FixedClock(new DateTimeOffset(2026, 7, 1, 14, 55, 0, TimeSpan.Zero)));
        IAdminOperationRunner runner = services.GetRequiredService<IAdminOperationRunner>();
        using CancellationTokenSource caller = new();
        caller.Cancel();

        AdminOperationExecutionResult<int> result = await runner.ExecuteAsync(
            new AdminOperationContext(
                AdminActor.System("actor"),
                AdminOperation.Create("auth.members.create", AdminPermission.Create("auth.members.create")),
                "tenant-a",
                RequireTenant: true),
            _ => Task.FromResult(Result.Success(42)),
            caller.Token);

        Assert.Equal(AdminOperationExecutionStatus.Succeeded, result.Status);
        Assert.False(audit.ReceivedCancellationToken.IsCancellationRequested);
        Assert.Single(audit.Records);
    }

    [Fact]
    public async Task Canceled_action_is_audited_before_cancellation_is_propagated()
    {
        RecordingAuditSink audit = new();
        ServiceProvider services = CreateServices(
            new RecordingAuthorizationService(),
            audit,
            new FixedClock(new DateTimeOffset(2026, 7, 1, 14, 57, 0, TimeSpan.Zero)));
        IAdminOperationRunner runner = services.GetRequiredService<IAdminOperationRunner>();
        using CancellationTokenSource caller = new();
        caller.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.ExecuteAsync(
            new AdminOperationContext(
                AdminActor.System("actor"),
                AdminOperation.Create("auth.members.create", AdminPermission.Create("auth.members.create")),
                "tenant-a",
                RequireTenant: true),
            _ => Task.FromCanceled<Result<int>>(caller.Token),
            caller.Token));

        AdminAuditRecord record = Assert.Single(audit.Records);
        Assert.Equal(AdminAuditResult.Canceled, record.Result);
        Assert.Equal(AdminErrors.OperationCanceled.Code, record.ErrorCode);
        Assert.False(audit.ReceivedCancellationToken.IsCancellationRequested);
    }

    [Fact]
    public async Task Tenant_context_is_cleared_before_each_operation()
    {
        RecordingAuditSink audit = new();
        EnabledTenantContext tenantContext = new();
        ServiceProvider services = CreateServices(
            new RecordingAuthorizationService(),
            audit,
            new FixedClock(new DateTimeOffset(2026, 7, 1, 15, 0, 0, TimeSpan.Zero)),
            tenantContext: tenantContext);
        IAdminOperationRunner runner = services.GetRequiredService<IAdminOperationRunner>();

        AdminOperationExecutionResult<int> first = await runner.ExecuteAsync<int>(
            new AdminOperationContext(
                AdminActor.System("actor"),
                AdminOperation.Create("auth.members.create", AdminPermission.Create("auth.members.create")),
                "tenant-a",
                RequireTenant: true),
            _ => Task.FromResult(Result.Success(42)),
            CancellationToken.None);

        AdminOperationExecutionResult<int> second = await runner.ExecuteAsync<int>(
            new AdminOperationContext(
                AdminActor.System("actor"),
                AdminOperation.Create("admin.roles.list", AdminPermission.Create("admin.roles.list")),
                TenantId: null,
                RequireTenant: false),
            _ => Task.FromResult(Result.Success(43)),
            CancellationToken.None);

        Assert.Equal(AdminOperationExecutionStatus.Succeeded, first.Status);
        Assert.Equal(AdminOperationExecutionStatus.Succeeded, second.Status);
        Assert.Null(tenantContext.TenantId);
    }

    private static ServiceProvider CreateServices(
        IAdminAuthorizationService authorization,
        IAdminAuditSink? audit,
        ISystemClock clock,
        bool addThrowingLogger = false,
        EnabledTenantContext? tenantContext = null)
    {
        FixedIdGenerator idGenerator = new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        ServiceCollection services = new();
        tenantContext ??= new EnabledTenantContext();

        services.AddLogging(builder =>
        {
            if (addThrowingLogger)
            {
                builder.AddProvider(new ThrowingLoggerProvider());
            }
        });

        services
            .AddSingleton(tenantContext)
            .AddScoped<ITenantContext>(provider => provider.GetRequiredService<EnabledTenantContext>())
            .AddScoped<ITenantContextAccessor>(provider => provider.GetRequiredService<EnabledTenantContext>())
            .AddSingleton(clock)
            .AddSingleton<IIdGenerator>(idGenerator)
            .AddScoped(_ => authorization)
            .AddGmaAdministration();

        if (audit is not null)
        {
            services.AddScoped(_ => audit);
        }

        return services.BuildServiceProvider();
    }

    private sealed class EnabledTenantContext : ITenantContextAccessor
    {
        private string? scopeId;

        public string? InitialScopeId
        {
            set => this.scopeId = value;
        }

        public bool IsEnabled => true;
        public string? TenantId => this.scopeId;
        public void SetTenant(string scopeId) => this.scopeId = scopeId;
        public void ClearTenant() => this.scopeId = null;
    }

    private sealed class RecordingAuthorizationService : IAdminAuthorizationService
    {
        public int CallCount { get; private set; }

        public Task<AdminAuthorizationResult> AuthorizeAsync(
            AdminActor actor,
            AdminPermission permission,
            string? scopeId,
            CancellationToken cancellationToken)
        {
            this.CallCount++;
            return Task.FromResult(AdminAuthorizationResult.Allowed());
        }
    }

    private sealed class ThrowingAuthorizationService : IAdminAuthorizationService
    {
        public Task<AdminAuthorizationResult> AuthorizeAsync(
            AdminActor actor,
            AdminPermission permission,
            string? scopeId,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("rbac unavailable");
    }

    private sealed class FixedClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class FixedIdGenerator(Guid id) : IIdGenerator
    {
        public Guid Id => id;
        public Guid NewId() => this.Id;
    }

    private sealed class RecordingAuditSink : IAdminAuditSink
    {
        public List<AdminAuditRecord> Records { get; } = [];
        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task RecordAsync(AdminAuditRecord record, CancellationToken cancellationToken)
        {
            this.ReceivedCancellationToken = cancellationToken;
            this.Records.Add(record);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingAuditSink : IAdminAuditSink
    {
        public Task RecordAsync(AdminAuditRecord record, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Audit unavailable.");
    }

    private sealed class ThrowingLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new ThrowingLogger();

        public void Dispose()
        {
        }
    }

    private sealed class ThrowingLogger : ILogger
    {
        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull =>
            NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            throw new InvalidOperationException("Logger unavailable.");
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
