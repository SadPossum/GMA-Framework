namespace Gma.Framework.ProjectionRebuild;

using Gma.Framework.Naming;

internal sealed class ProjectionRebuildCheckpointStoreRegistry : IProjectionRebuildCheckpointStoreRegistry
{
    private readonly Dictionary<string, IProjectionRebuildCheckpointStore> storesByModule;

    public ProjectionRebuildCheckpointStoreRegistry(IEnumerable<IProjectionRebuildCheckpointStore> stores)
    {
        ArgumentNullException.ThrowIfNull(stores);

        ProjectionRebuildCheckpointStoreRegistration[] registrations = stores
            .Select(CreateRegistration)
            .ToArray();

        IGrouping<string, ProjectionRebuildCheckpointStoreRegistration>? duplicate = registrations
            .GroupBy(registration => registration.ModuleName, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"{duplicate.Count()} projection rebuild checkpoint stores are registered for module '{duplicate.Key}'.");
        }

        this.storesByModule = registrations.ToDictionary(
            registration => registration.ModuleName,
            registration => registration.Store,
            StringComparer.Ordinal);
    }

    public IProjectionRebuildCheckpointStore GetRequired(string moduleName)
    {
        string normalized = SharedModuleNames.Normalize(moduleName, nameof(moduleName));

        return this.storesByModule.TryGetValue(normalized, out IProjectionRebuildCheckpointStore? store)
            ? store
            : throw new InvalidOperationException(
                $"No projection rebuild checkpoint store is registered for module '{normalized}'.");
    }

    private static ProjectionRebuildCheckpointStoreRegistration CreateRegistration(IProjectionRebuildCheckpointStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        try
        {
            return new(
                SharedModuleNames.Normalize(store.ModuleName, nameof(IProjectionRebuildCheckpointStore.ModuleName)),
                store);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                $"Projection rebuild checkpoint store '{store.GetType().FullName}' has an invalid module name.",
                exception);
        }
    }

    private sealed record ProjectionRebuildCheckpointStoreRegistration(
        string ModuleName,
        IProjectionRebuildCheckpointStore Store);
}
