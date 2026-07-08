namespace Gma.Framework.Tests;

using Gma.Framework.Domain;
using Gma.Framework.Domain.Models;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TenantModelTests
{
    [Fact]
    public void Tenant_aggregate_root_normalizes_tenant_id()
    {
        TestTenantAggregate aggregate = new(Guid.NewGuid(), " tenant-a ");

        Assert.Equal("tenant-a", aggregate.TenantId);
    }

    [Fact]
    public void Tenant_entity_normalizes_tenant_id()
    {
        TestTenantEntity entity = new(Guid.NewGuid(), " tenant-a ");

        Assert.Equal("tenant-a", entity.TenantId);
    }

    [Fact]
    public void Tenant_base_types_reject_invalid_tenant_ids()
    {
        Assert.Throws<ArgumentException>(() => new TestTenantAggregate(Guid.NewGuid(), " "));
        Assert.Throws<ArgumentException>(() => new TestTenantEntity(Guid.NewGuid(), "tenant with spaces"));
    }

    [Fact]
    public void Disable_tenant_filter_attribute_requires_reason()
    {
        Assert.Throws<ArgumentException>(() => new DisableTenantFilterAttribute(" "));

        DisableTenantFilterAttribute attribute = new("Projection rebuild reads are module-owned.");

        Assert.Equal("Projection rebuild reads are module-owned.", attribute.Reason);
    }

    private sealed class TestTenantAggregate(Guid id, string tenantId) : TenantAggregateRoot<Guid>(id, tenantId);

    private sealed class TestTenantEntity(Guid id, string tenantId) : TenantEntity<Guid>(id, tenantId);
}
