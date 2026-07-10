namespace Gma.Framework.Persistence.EntityFrameworkCore;

using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Gma.Framework.Domain;
using Gma.Framework.Naming;

public static class ScopedEntityTypeBuilderExtensions
{
    private static readonly MethodInfo ApplyScopeConventionsForEntityMethod =
        typeof(ScopedEntityTypeBuilderExtensions)
            .GetMethod(nameof(ApplyScopeConventionsForEntity), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static ModelBuilder ApplyScopeConventions<TContext>(
        this ModelBuilder modelBuilder,
        ScopeAwareDbContext<TContext> context)
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

            if (!typeof(IScopedEntity).IsAssignableFrom(clrType))
            {
                continue;
            }

            ApplyScopeConventionsForEntityMethod
                .MakeGenericMethod(clrType, typeof(TContext))
                .Invoke(null, [modelBuilder, context]);
        }

        return modelBuilder;
    }

    public static EntityTypeBuilder<TEntity> ConfigureScopeId<TEntity>(
        this EntityTypeBuilder<TEntity> builder)
        where TEntity : class, IScopedEntity
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Property(entity => entity.ScopeId)
            .HasMaxLength(ScopeIds.MaxLength)
            .IsRequired();

        return builder;
    }

    public static EntityTypeBuilder<TEntity> ApplyScopeFilter<TEntity, TContext>(
        this EntityTypeBuilder<TEntity> builder,
        ScopeAwareDbContext<TContext> context)
        where TEntity : class, IScopedEntity
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(context);

        Expression<Func<TEntity, bool>> filter =
            entity => !context.ScopeFilterEnabled || entity.ScopeId == context.CurrentScopeId;

        builder.HasQueryFilter(ScopeFilterNames.ScopeFilter, filter);
        return builder;
    }

    private static void ApplyScopeConventionsForEntity<TEntity, TContext>(
        ModelBuilder modelBuilder,
        ScopeAwareDbContext<TContext> context)
        where TEntity : class, IScopedEntity
        where TContext : DbContext
        => modelBuilder.Entity<TEntity>()
            .ConfigureScopeId()
            .ApplyScopeFilter(context);

    private static void ValidateClassification(Type clrType)
    {
        bool scopeScoped = typeof(IScopedEntity).IsAssignableFrom(clrType);
        bool global = clrType.GetCustomAttribute<GlobalEntityAttribute>() is not null;
        DisableScopeFilterAttribute? disabled = clrType.GetCustomAttribute<DisableScopeFilterAttribute>();

        if (scopeScoped && global)
        {
            throw new InvalidOperationException(
                $"{clrType.FullName} cannot be both scope-scoped and global.");
        }

        if (disabled is not null && string.IsNullOrWhiteSpace(disabled.Reason))
        {
            throw new InvalidOperationException(
                $"{clrType.FullName} disables the scope filter without a reason.");
        }
    }
}
