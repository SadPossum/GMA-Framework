namespace Gma.Framework.AccessControl.AspNetCore;

using Gma.Framework.Observability;

internal sealed class AccessControlSecuritySignalDefinitions
    : ISecuritySignalDefinitionSource
{
    public static readonly SecuritySignalDefinition SubjectMissing = new(
        "access-control.subject-missing",
        SecuritySignalCategory.Authentication,
        SecuritySignalSeverity.Warning);

    public static readonly SecuritySignalDefinition ScopeDenied = new(
        "access-control.scope-denied",
        SecuritySignalCategory.Authorization,
        SecuritySignalSeverity.Warning);

    public static readonly SecuritySignalDefinition ScopeResolutionFailed = new(
        "access-control.scope-resolution-failed",
        SecuritySignalCategory.Authorization,
        SecuritySignalSeverity.Critical);

    public static readonly SecuritySignalDefinition PermissionDenied = new(
        "access-control.permission-denied",
        SecuritySignalCategory.Authorization,
        SecuritySignalSeverity.Warning);

    private static readonly SecuritySignalDefinition[] All =
    [
        SubjectMissing,
        ScopeDenied,
        ScopeResolutionFailed,
        PermissionDenied
    ];

    public IReadOnlyCollection<SecuritySignalDefinition> Definitions => All;
}
