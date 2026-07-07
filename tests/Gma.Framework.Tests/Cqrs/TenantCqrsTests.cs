namespace Gma.Framework.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Gma.Framework.Cqrs.Infrastructure;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Observability;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Cqrs;
using Gma.Framework.Tenancy.Infrastructure;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TenantCqrsTests
{
    [Fact]
    public void Tenant_cqrs_logging_registers_scope_contributor()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Tenancy:Enabled"] = "true";

        builder.AddTenancyInfrastructure();
        builder.AddTenantCqrsLogging();
        ModuleCompositionValidationResult validation = builder.ValidateModuleComposition();

        using IHost host = builder.Build();
        using IServiceScope scope = host.Services.CreateScope();
        ICqrsLogScopeContributor contributor = Assert.Single(scope.ServiceProvider.GetServices<ICqrsLogScopeContributor>());
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant("tenant-a");

        Dictionary<string, object?> scopeProperties = [];
        contributor.Enrich(
            new CqrsLogScopeContext("auth", "RegisterMemberCommand", typeof(TestCommand), CqrsRequestKind.Command),
            scopeProperties);

        Assert.True(validation.IsValid, validation.Report);
        Assert.Equal("tenant-a", scopeProperties[ObservabilityLogPropertyNames.TenantId]);
    }

    [Fact]
    public void Tenant_cqrs_logging_requires_tenancy_context_provider()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.AddTenantCqrsLogging();

        ModuleCompositionValidationException exception = Assert.Throws<ModuleCompositionValidationException>(
            () => builder.ValidateModuleComposition());

        Assert.Contains("tenancy.context", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ITenantContext), exception.Message, StringComparison.Ordinal);
    }

    private sealed record TestCommand;
}
