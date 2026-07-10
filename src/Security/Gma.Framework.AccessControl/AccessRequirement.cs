namespace Gma.Framework.AccessControl;

using System.Collections.ObjectModel;
using Gma.Framework.Permissions;

public sealed record AccessRequirement
{
    private readonly ReadOnlyDictionary<string, string> resourceMetadata;

    public AccessRequirement(
        AccessSubject subject,
        PermissionCode permission,
        AccessScope scope,
        IReadOnlyDictionary<string, string>? resourceMetadata = null)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(permission);
        ArgumentNullException.ThrowIfNull(scope);

        this.Subject = subject;
        this.Permission = permission;
        this.Scope = scope;
        this.resourceMetadata = NormalizeResourceMetadata(resourceMetadata);
    }

    public AccessSubject Subject { get; }
    public PermissionCode Permission { get; }
    public AccessScope Scope { get; }
    public IReadOnlyDictionary<string, string> ResourceMetadata => this.resourceMetadata;

    private static ReadOnlyDictionary<string, string> NormalizeResourceMetadata(
        IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
        }

        Dictionary<string, string> normalized = new(StringComparer.Ordinal);
        foreach ((string key, string value) in metadata)
        {
            string normalizedKey = AccessText.NormalizeIdentifier(key, 128, "Access resource metadata key", nameof(metadata));
            string normalizedValue = AccessText.NormalizeIdentifier(value, 512, "Access resource metadata value", nameof(metadata));
            normalized.Add(normalizedKey, normalizedValue);
        }

        return new ReadOnlyDictionary<string, string>(normalized);
    }
}
