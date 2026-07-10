namespace Gma.Framework.Tests;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Gma.Framework.Domain;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Scoping;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TenantModelConventionTests
{
    [Fact]
    public async Task Apply_tenant_conventions_filters_per_context_tenant()
    {
        string databaseName = Guid.NewGuid().ToString("N");
        await using (TestTenantDbContext seed = CreateDbContext(databaseName, enabled: false, scopeId: null))
        {
            seed.TenantRecords.Add(new TestTenantRecord(Guid.NewGuid(), "tenant-a", "A"));
            seed.TenantRecords.Add(new TestTenantRecord(Guid.NewGuid(), "tenant-b", "B"));
            seed.GlobalRecords.Add(new TestGlobalRecord { Id = Guid.NewGuid(), Name = "global" });
            await seed.SaveChangesAsync();
        }

        await using TestTenantDbContext tenantA = CreateDbContext(databaseName, enabled: true, scopeId: "tenant-a");
        await using TestTenantDbContext tenantB = CreateDbContext(databaseName, enabled: true, scopeId: "tenant-b");

        Assert.Equal(["A"], await tenantA.TenantRecords.Select(record => record.Name).ToListAsync());
        Assert.Equal(["B"], await tenantB.TenantRecords.Select(record => record.Name).ToListAsync());
        Assert.Equal(1, await tenantA.GlobalRecords.CountAsync());
        Assert.Equal(1, await tenantB.GlobalRecords.CountAsync());
    }

    [Fact]
    public void Apply_tenant_conventions_configures_tenant_property_and_named_filter()
    {
        using TestTenantDbContext dbContext = CreateDbContext(Guid.NewGuid().ToString("N"), enabled: true, scopeId: "tenant-a");
        IEntityType entityType = dbContext.Model.FindEntityType(typeof(TestTenantRecord)) ??
            throw new InvalidOperationException("Tenant record was not configured.");

        Assert.Equal(
            ScopeIds.MaxLength,
            entityType.FindProperty(nameof(TestTenantRecord.ScopeId))?.GetMaxLength());
        Assert.Contains(ScopeFilterNames.ScopeFilter, entityType.GetDeclaredQueryFilters().Select(filter => filter.Key));
    }

    [Fact]
    public async Task Write_guard_allows_matching_tenant_writes()
    {
        await using TestTenantDbContext dbContext = CreateDbContext(
            Guid.NewGuid().ToString("N"),
            enabled: true,
            scopeId: "tenant-a");

        dbContext.TenantRecords.Add(new TestTenantRecord(Guid.NewGuid(), "tenant-a", "A"));

        await dbContext.SaveChangesAsync();

        Assert.Equal(1, await dbContext.TenantRecords.CountAsync());
    }

    [Fact]
    public async Task Write_guard_allows_global_writes_without_active_tenant()
    {
        await using TestTenantDbContext dbContext = CreateDbContext(
            Guid.NewGuid().ToString("N"),
            enabled: true,
            scopeId: null);

        dbContext.GlobalRecords.Add(new TestGlobalRecord { Id = Guid.NewGuid(), Name = "global" });

        await dbContext.SaveChangesAsync();

        Assert.Equal(1, await dbContext.GlobalRecords.CountAsync());
    }

    [Fact]
    public async Task Write_guard_rejects_mismatched_tenant_writes()
    {
        await using TestTenantDbContext dbContext = CreateDbContext(
            Guid.NewGuid().ToString("N"),
            enabled: true,
            scopeId: "tenant-a");

        dbContext.TenantRecords.Add(new TestTenantRecord(Guid.NewGuid(), "tenant-b", "B"));

        ScopeWriteGuardException exception = await Assert.ThrowsAsync<ScopeWriteGuardException>(
            () => dbContext.SaveChangesAsync());

        Assert.Contains("active scope is 'tenant-a'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Write_guard_rejects_missing_invalid_or_unnormalized_tenant_ids()
    {
        await using TestTenantDbContext dbContext = CreateDbContext(
            Guid.NewGuid().ToString("N"),
            enabled: false,
            scopeId: null);

        dbContext.MutableTenantRecords.Add(new MutableTenantRecord
        {
            Id = Guid.NewGuid(),
            ScopeId = " tenant-a ",
            Name = "A"
        });

        ScopeWriteGuardException exception = await Assert.ThrowsAsync<ScopeWriteGuardException>(
            () => dbContext.SaveChangesAsync());

        Assert.Contains("invalid or unnormalized scope id", exception.Message, StringComparison.Ordinal);
    }

    private static TestTenantDbContext CreateDbContext(string databaseName, bool enabled, string? scopeId)
    {
        DbContextOptions<TestTenantDbContext> options = new DbContextOptionsBuilder<TestTenantDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new TestTenantDbContext(options, new TestTenantContext(enabled, scopeId));
    }

    private sealed class TestTenantDbContext(
        DbContextOptions<TestTenantDbContext> options,
        IScopeContext scopeContext) : ScopeAwareDbContext<TestTenantDbContext>(options, scopeContext)
    {
        public DbSet<TestTenantRecord> TenantRecords => this.Set<TestTenantRecord>();
        public DbSet<MutableTenantRecord> MutableTenantRecords => this.Set<MutableTenantRecord>();
        public DbSet<TestGlobalRecord> GlobalRecords => this.Set<TestGlobalRecord>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestTenantRecord>().ToTable("tenant_records");
            modelBuilder.Entity<MutableTenantRecord>().ToTable("mutable_tenant_records");
            modelBuilder.Entity<TestGlobalRecord>().ToTable("global_records");
            this.ApplyScopeConventions(modelBuilder);
        }
    }

    private sealed class TestTenantContext(bool enabled, string? scopeId) : IScopeContext
    {
        public bool IsEnabled { get; } = enabled;
        public string? ScopeId { get; } = scopeId;
    }

    private sealed class TestTenantRecord(Guid id, string scopeId, string name) : ScopedEntity<Guid>(id, scopeId)
    {
        public string Name { get; private set; } = name;
    }

    private sealed class MutableTenantRecord : IScopedEntity
    {
        public Guid Id { get; set; }
        public string ScopeId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    [GlobalEntity]
    private sealed class TestGlobalRecord
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
