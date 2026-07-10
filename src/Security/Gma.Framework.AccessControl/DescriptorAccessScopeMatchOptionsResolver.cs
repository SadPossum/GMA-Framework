namespace Gma.Framework.AccessControl;

using Gma.Framework.Permissions;

internal sealed class DescriptorAccessScopeMatchOptionsResolver(
    IEnumerable<AccessPermissionScopePolicyRegistration> registrations)
    : IAccessScopeMatchOptionsResolver
{
    private readonly Dictionary<string, AccessScopeMatchOptions> optionsByPermission = registrations
        .ToDictionary(
            registration => registration.Permission.Value,
            registration => registration.MatchOptions,
            StringComparer.Ordinal);

    public AccessScopeMatchOptions Resolve(PermissionCode permission)
    {
        ArgumentNullException.ThrowIfNull(permission);

        return this.optionsByPermission.TryGetValue(permission.Value, out AccessScopeMatchOptions? options)
            ? options
            : new AccessScopeMatchOptions();
    }
}

internal sealed record AccessPermissionScopePolicyRegistration
{
    public AccessPermissionScopePolicyRegistration(ModulePermissionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        this.Permission = descriptor.Permission;
        this.MatchOptions = new AccessScopeMatchOptions(
            descriptor.ScopeGrantPolicy.AllowAncestorScopeGrants,
            descriptor.ScopeGrantPolicy.AllowGlobalScopeGrant);
    }

    public PermissionCode Permission { get; }
    public AccessScopeMatchOptions MatchOptions { get; }
}
