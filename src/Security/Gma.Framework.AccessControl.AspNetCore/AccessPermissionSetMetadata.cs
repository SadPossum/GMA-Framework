namespace Gma.Framework.AccessControl.AspNetCore;

using System.Collections.ObjectModel;

public sealed record AccessPermissionSetMetadata
{
    public const int MaxRequirements = 16;

    private readonly ReadOnlyCollection<AccessPermissionMetadata> requirements;

    public AccessPermissionSetMetadata(IEnumerable<AccessPermissionMetadata> requirements)
    {
        ArgumentNullException.ThrowIfNull(requirements);

        AccessPermissionMetadata[] materialized = [.. requirements];
        if (materialized.Length is 0 or > MaxRequirements ||
            materialized.Any(requirement => requirement is null))
        {
            throw new ArgumentException(
                $"A permission set requires between 1 and {MaxRequirements} non-null requirements.",
                nameof(requirements));
        }

        if (materialized.Distinct().Count() != materialized.Length)
        {
            throw new ArgumentException(
                "A permission set cannot contain duplicate requirements.",
                nameof(requirements));
        }

        this.requirements = Array.AsReadOnly(materialized);
    }

    public IReadOnlyList<AccessPermissionMetadata> Requirements => this.requirements;
}
