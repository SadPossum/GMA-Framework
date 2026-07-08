namespace Gma.Framework.Persistence.EntityFrameworkCore;

using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Gma.Framework.Domain;
using Gma.Framework.Naming;

public static class TenantEntityTypeBuilderExtensions
{
    private static readonly MethodInfo ApplyTenantConventionsForEntityMethod =
        typeof(TenantEntityTypeBuilderExtensions)
            .GetMethod(nameof(ApplyTenantConventionsForEntity), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static ModelBuilder ApplyTenantConventions<TContext>(
        this ModelBuilder modelBuilder,
        TenantAwareDbContext<TContext> context)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(context);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            Type? clrType = entityType.ClrType;
            if (clrType is null || entityType.IsOwned())
            {
                continue;
            }

            ValidateClassification(clrType);

            if (!typeof(ITenantScoped).IsAssignableFrom(clrType))
            {
                continue;
            }

            ApplyTenantConventionsForEntityMethod
                .MakeGenericMethod(clrType, typeof(TContext))
                .Invoke(null, [modelBuilder, context]);
        }

        return modelBuilder;
    }

    public static EntityTypeBuilder<TEntity> ConfigureTenantId<TEntity>(
        this EntityTypeBuilder<TEntity> builder)
        where TEntity : class, ITenantScoped
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Property(entity => entity.TenantId)
            .HasMaxLength(TenantIds.MaxLength)
            .IsRequired();

        return builder;
    }

    public static EntityTypeBuilder<TEntity> ApplyTenantFilter<TEntity, TContext>(
        this EntityTypeBuilder<TEntity> builder,
        TenantAwareDbContext<TContext> context)
        where TEntity : class, ITenantScoped
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(context);

        Expression<Func<TEntity, bool>> filter =
            entity => !context.TenantFilterEnabled || entity.TenantId == context.CurrentTenantId;

        builder.HasQueryFilter(TenantFilterNames.TenantFilter, filter);
        return builder;
    }

    private static void ApplyTenantConventionsForEntity<TEntity, TContext>(
        ModelBuilder modelBuilder,
        TenantAwareDbContext<TContext> context)
        where TEntity : class, ITenantScoped
        where TContext : DbContext
        => modelBuilder.Entity<TEntity>()
            .ConfigureTenantId()
            .ApplyTenantFilter(context);

    private static void ValidateClassification(Type clrType)
    {
        bool tenantScoped = typeof(ITenantScoped).IsAssignableFrom(clrType);
        bool global = clrType.GetCustomAttribute<GlobalEntityAttribute>() is not null;
        DisableTenantFilterAttribute? disabled = clrType.GetCustomAttribute<DisableTenantFilterAttribute>();

        if (tenantScoped && global)
        {
            throw new InvalidOperationException(
                $"{clrType.FullName} cannot be both tenant-scoped and global.");
        }

        if (disabled is not null && string.IsNullOrWhiteSpace(disabled.Reason))
        {
            throw new InvalidOperationException(
                $"{clrType.FullName} disables the tenant filter without a reason.");
        }
    }
}
