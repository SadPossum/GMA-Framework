namespace Gma.Framework.ProjectionRebuild;

using Gma.Framework.Naming;

internal sealed class ProjectionRebuildTransactionBoundaryRegistry : IProjectionRebuildTransactionBoundaryRegistry
{
    private readonly Dictionary<string, IProjectionRebuildTransactionBoundary> boundariesByModule;

    public ProjectionRebuildTransactionBoundaryRegistry(IEnumerable<IProjectionRebuildTransactionBoundary> boundaries)
    {
        ArgumentNullException.ThrowIfNull(boundaries);

        ProjectionRebuildTransactionBoundaryRegistration[] registrations = boundaries
            .Select(CreateRegistration)
            .ToArray();

        IGrouping<string, ProjectionRebuildTransactionBoundaryRegistration>? duplicate = registrations
            .GroupBy(registration => registration.ModuleName, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"{duplicate.Count()} projection rebuild transaction boundaries are registered for module '{duplicate.Key}'.");
        }

        this.boundariesByModule = registrations.ToDictionary(
            registration => registration.ModuleName,
            registration => registration.Boundary,
            StringComparer.Ordinal);
    }

    public IProjectionRebuildTransactionBoundary? GetOptional(string moduleName)
    {
        string normalized = SharedModuleNames.Normalize(moduleName, nameof(moduleName));
        return this.boundariesByModule.GetValueOrDefault(normalized);
    }

    private static ProjectionRebuildTransactionBoundaryRegistration CreateRegistration(
        IProjectionRebuildTransactionBoundary boundary)
    {
        ArgumentNullException.ThrowIfNull(boundary);

        try
        {
            return new(
                SharedModuleNames.Normalize(
                    boundary.ModuleName,
                    nameof(IProjectionRebuildTransactionBoundary.ModuleName)),
                boundary);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                $"Projection rebuild transaction boundary '{boundary.GetType().FullName}' has an invalid module name.",
                exception);
        }
    }

    private sealed record ProjectionRebuildTransactionBoundaryRegistration(
        string ModuleName,
        IProjectionRebuildTransactionBoundary Boundary);
}
