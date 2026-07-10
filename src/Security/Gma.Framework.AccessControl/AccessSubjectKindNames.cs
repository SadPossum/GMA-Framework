namespace Gma.Framework.AccessControl;

using System.Diagnostics.CodeAnalysis;

public static class AccessSubjectKindNames
{
    public const string User = "user";
    public const string AdminActor = "admin-actor";
    public const string Service = "service";
    public const string System = "system";

    public static string GetName(AccessSubjectKind kind) => kind switch
    {
        AccessSubjectKind.User => User,
        AccessSubjectKind.AdminActor => AdminActor,
        AccessSubjectKind.Service => Service,
        AccessSubjectKind.System => System,
        _ => throw new ArgumentException("Access subject kind must be a defined non-unknown value.", nameof(kind))
    };

    public static bool TryParse(string? value, out AccessSubjectKind kind)
    {
        kind = AccessSubjectKind.Unknown;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        kind = value.Trim().ToLowerInvariant() switch
        {
            User => AccessSubjectKind.User,
            AdminActor => AccessSubjectKind.AdminActor,
            Service => AccessSubjectKind.Service,
            System => AccessSubjectKind.System,
            _ => AccessSubjectKind.Unknown
        };

        return kind != AccessSubjectKind.Unknown;
    }

    public static bool TryCreate(
        string? kind,
        string? id,
        [NotNullWhen(true)] out AccessSubject? subject)
    {
        subject = null;
        return TryParse(kind, out AccessSubjectKind parsedKind) &&
               AccessSubject.TryCreate(parsedKind, id, out subject);
    }
}
