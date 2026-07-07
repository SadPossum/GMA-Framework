namespace Gma.Framework.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Modules;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Infrastructure;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Infrastructure;
using Gma.Framework.Tenancy.Tasks;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TenantTaskExecutionContextTests
{
    [Fact]
    public void Tenant_task_execution_context_registers_contributor_and_advertises_feature()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.AddTenancyInfrastructure();
        builder.AddTenantTaskExecutionContext();

        ModuleCompositionValidationResult result = builder.ValidateModuleComposition();

        Assert.True(result.IsValid, result.Report);
        Assert.Contains("tasks.tenant-scope by Gma.Framework.Tenancy.Tasks", result.Report, StringComparison.Ordinal);
        Assert.Single(
            builder.Services,
            descriptor => descriptor.ServiceType == typeof(ITaskExecutionContextContributor));
    }

    [Fact]
    public void Tenant_task_execution_context_requires_tenancy_context_provider()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.AddTenantTaskExecutionContext();

        ModuleCompositionValidationException exception = Assert.Throws<ModuleCompositionValidationException>(
            () => builder.ValidateModuleComposition());

        Assert.Contains("tenancy.context", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ITenantContextAccessor", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tenant_task_execution_context_sets_and_clears_tenant_for_tenant_scoped_task()
    {
        using IHost host = BuildTenantTaskHost();
        using IServiceScope scope = host.Services.CreateScope();
        ITaskExecutionContextContributor contributor = scope.ServiceProvider
            .GetServices<ITaskExecutionContextContributor>()
            .Single();
        ITenantContextAccessor tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        TaskExecutionContextPreparationContext context = CreatePreparationContext(tenantId: "tenant-a", tenantScoped: true);

        TaskExecutionContextPreparationResult result = await contributor
            .PrepareAsync(context, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("tenant-a", tenantContext.TenantId);

        await contributor.CleanupAsync(context, CancellationToken.None);

        Assert.Equal("default", tenantContext.TenantId);
    }

    [Fact]
    public async Task Tenant_task_execution_context_rejects_missing_tenant_for_tenant_scoped_task()
    {
        using IHost host = BuildTenantTaskHost();
        using IServiceScope scope = host.Services.CreateScope();
        ITaskExecutionContextContributor contributor = scope.ServiceProvider
            .GetServices<ITaskExecutionContextContributor>()
            .Single();
        TaskExecutionContextPreparationContext context = CreatePreparationContext(tenantId: null, tenantScoped: true);

        TaskExecutionContextPreparationResult result = await contributor
            .PrepareAsync(context, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("has no tenant id", result.ErrorMessage!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tenant_task_execution_context_ignores_global_task()
    {
        using IHost host = BuildTenantTaskHost();
        using IServiceScope scope = host.Services.CreateScope();
        ITaskExecutionContextContributor contributor = scope.ServiceProvider
            .GetServices<ITaskExecutionContextContributor>()
            .Single();
        ITenantContextAccessor tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        tenantContext.SetTenant("existing-tenant");
        TaskExecutionContextPreparationContext context = CreatePreparationContext(tenantId: null, tenantScoped: false);

        TaskExecutionContextPreparationResult result = await contributor
            .PrepareAsync(context, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("default", tenantContext.TenantId);
    }

    private static IHost BuildTenantTaskHost()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Tenancy:Enabled"] = "true";
        builder.AddTenancyInfrastructure();
        builder.AddTenantTaskExecutionContext();
        return builder.Build();
    }

    private static TaskExecutionContextPreparationContext CreatePreparationContext(string? tenantId, bool tenantScoped)
    {
        DateTimeOffset leasedAtUtc = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        TaskRunLease lease = new(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "catalog",
            "rebuild-search",
            TaskWorkerGroups.Default,
            "worker-01",
            "node-01",
            "{}",
            attempt: 1,
            leasedAtUtc,
            leasedAtUtc.AddMinutes(5),
            tenantId);
        TaskHandlerRegistration registration = TaskHandlerRegistration.Create<TestPayload, TestHandler>(
            "catalog",
            "rebuild-search",
            metadata: tenantScoped
                ? [TenantScopeMetadataItem.Instance]
                : Array.Empty<ModuleMetadataItem>());

        return new TaskExecutionContextPreparationContext(
            lease,
            registration,
            lease.CreateExecutionContext());
    }

    private sealed record TestPayload : ITaskPayload;

    private sealed class TestHandler : ITaskHandler<TestPayload>
    {
        public Task HandleAsync(
            TestPayload payload,
            TaskExecutionContext context,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
