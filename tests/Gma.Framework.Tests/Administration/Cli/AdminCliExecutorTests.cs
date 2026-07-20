namespace Gma.Framework.Tests;

using Microsoft.Extensions.DependencyInjection;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Cli;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Tenancy;
using System.CommandLine;
using System.CommandLine.Parsing;
using Xunit;

[Trait("Category", "Unit")]
[Collection(ConsoleTestIsolation.Name)]
public sealed class AdminCliExecutorTests
{
    [Fact]
    public async Task Invalid_actor_returns_validation_failure_without_running_operation()
    {
        ServiceProvider services = new ServiceCollection()
            .AddGmaAdministrationCli()
            .BuildServiceProvider();
        AdminCliGlobalOptions options = services.GetRequiredService<AdminCliGlobalOptions>();
        RootCommand root = CreateRoot(options);
        ParseResult parseResult = root.Parse(["--actor", "actor 1"]);
        AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
        int actionExecutions = 0;

        using StringWriter error = new();
        TextWriter originalError = Console.Error;
        Console.SetError(error);

        try
        {
            int exitCode = await executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create("admin.test", AdminPermission.Create("admin.test")),
                tenantId: null,
                requireTenant: false,
                (_, _) =>
                {
                    actionExecutions++;
                    return Task.FromResult(Result.Success(Unit.Value));
                },
                CancellationToken.None);

            Assert.Equal(AdminExitCodes.ValidationFailed, exitCode);
        }
        finally
        {
            Console.SetError(originalError);
        }

        Assert.Equal(0, actionExecutions);
        Assert.Contains(AdminActor.InvalidIdMessage, error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_output_returns_validation_failure_without_running_operation()
    {
        ServiceProvider services = new ServiceCollection()
            .AddGmaAdministrationCli()
            .BuildServiceProvider();
        AdminCliGlobalOptions options = services.GetRequiredService<AdminCliGlobalOptions>();
        RootCommand root = CreateRoot(options);
        ParseResult parseResult = root.Parse(["--actor", "actor", "--output", "xml"]);
        AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
        int actionExecutions = 0;

        using StringWriter error = new();
        TextWriter originalError = Console.Error;
        Console.SetError(error);

        try
        {
            int exitCode = await executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create("admin.test", AdminPermission.Create("admin.test")),
                tenantId: null,
                requireTenant: false,
                (_, _) =>
                {
                    actionExecutions++;
                    return Task.FromResult(Result.Success(Unit.Value));
                },
                CancellationToken.None);

            Assert.Equal(AdminExitCodes.ValidationFailed, exitCode);
        }
        finally
        {
            Console.SetError(originalError);
        }

        Assert.Equal(0, actionExecutions);
        Assert.Contains(AdminCliOutput.InvalidOutputMessage, error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Successful_operation_with_failed_audit_returns_partial_success_exit_code()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddScoped<ITenantContextAccessor, DisabledTenantContext>();
        services.AddSingleton<ISystemClock, FixedClock>();
        services.AddSingleton<IIdGenerator, RandomIdGenerator>();
        services.AddGmaAdministrationCli();
        services.AddScoped<IAdminAuthorizationService, AllowAllAuthorizationService>();
        services.AddScoped<IAdminAuditSink, ThrowingAuditSink>();
        using ServiceProvider provider = services.BuildServiceProvider();
        AdminCliGlobalOptions options = provider.GetRequiredService<AdminCliGlobalOptions>();
        RootCommand root = CreateRoot(options);
        ParseResult parseResult = root.Parse(["--actor", "actor"]);
        AdminCliExecutor executor = provider.GetRequiredService<AdminCliExecutor>();
        using StringWriter error = new();
        TextWriter originalError = Console.Error;
        Console.SetError(error);

        try
        {
            int exitCode = await executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create("admin.test", AdminPermission.Create("admin.test")),
                tenantId: null,
                requireTenant: false,
                (_, _) => Task.FromResult(Result.Success(Unit.Value)),
                CancellationToken.None);

            Assert.Equal(AdminExitCodes.AuditFailed, exitCode);
        }
        finally
        {
            Console.SetError(originalError);
        }

        Assert.Contains("Admin audit failed.", error.ToString(), StringComparison.Ordinal);
    }

    private static RootCommand CreateRoot(AdminCliGlobalOptions options)
    {
        RootCommand root = new("admin");
        root.Options.Add(options.ActorOption);
        root.Options.Add(options.OutputOption);
        return root;
    }

    private sealed class AllowAllAuthorizationService : IAdminAuthorizationService
    {
        public Task<AdminAuthorizationResult> AuthorizeAsync(
            AdminActor actor,
            AdminPermission permission,
            string? tenantId,
            CancellationToken cancellationToken) =>
            Task.FromResult(AdminAuthorizationResult.Allowed());
    }

    private sealed class ThrowingAuditSink : IAdminAuditSink
    {
        public Task RecordAsync(AdminAuditRecord record, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Audit unavailable.");
    }

    private sealed class DisabledTenantContext : ITenantContextAccessor
    {
        public bool IsEnabled => false;
        public string? TenantId => null;
        public void SetTenant(string tenantId) { }
        public void ClearTenant() { }
    }

    private sealed class FixedClock : ISystemClock
    {
        public DateTimeOffset UtcNow => new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class RandomIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }
}
