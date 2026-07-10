namespace Gma.Framework.Persistence.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Gma.Framework.Domain;
using Gma.Framework.Naming;
using Gma.Framework.Scoping;

public static class ScopeWriteGuard
{
    public static void ValidateScopedWrites(this ChangeTracker changeTracker, IScopeContext scopeContext)
    {
        ArgumentNullException.ThrowIfNull(changeTracker);
        ArgumentNullException.ThrowIfNull(scopeContext);

        EntityEntry<IScopedEntity>[] scopedWrites = changeTracker
                     .Entries<IScopedEntity>()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
                     .ToArray();

        if (scopedWrites.Length == 0)
        {
            return;
        }

        string? activeScopeId = null;
        if (scopeContext.IsEnabled &&
            !ScopeIds.TryNormalize(scopeContext.ScopeId, out activeScopeId))
        {
            throw new ScopeWriteGuardException("Scope-aware writes require a valid active scope id.");
        }

        foreach (EntityEntry<IScopedEntity> entry in scopedWrites)
        {
            string entityName = entry.Metadata.ClrType.FullName ?? entry.Metadata.ClrType.Name;
            string scopeId = entry.Entity.ScopeId;

            if (!ScopeIds.TryNormalize(scopeId, out string? normalizedScopeId) ||
                !string.Equals(normalizedScopeId, scopeId, StringComparison.Ordinal))
            {
                throw new ScopeWriteGuardException(
                    $"{entityName} has an invalid or unnormalized scope id.");
            }

            if (scopeContext.IsEnabled &&
                !string.Equals(normalizedScopeId, activeScopeId, StringComparison.Ordinal))
            {
                throw new ScopeWriteGuardException(
                    $"{entityName} belongs to scope '{normalizedScopeId}', but the active scope is '{activeScopeId}'.");
            }
        }
    }
}
