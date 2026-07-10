namespace Gma.Framework.AccessControl;

using System.Diagnostics.CodeAnalysis;
using Gma.Framework.Naming;

public sealed record AccessSubject
{
    public const int IdMaxLength = 256;

    public AccessSubject(AccessSubjectKind kind, string id)
    {
        if (kind == AccessSubjectKind.Unknown || !Enum.IsDefined(kind))
        {
            throw new ArgumentException("Access subject kind must be a defined non-unknown value.", nameof(kind));
        }

        this.Kind = kind;
        this.Id = AccessText.NormalizeIdentifier(id, IdMaxLength, "Access subject id", nameof(id));
    }

    public AccessSubjectKind Kind { get; }
    public string Id { get; }

    public static AccessSubject User(string id) =>
        new(AccessSubjectKind.User, id);

    public static AccessSubject AdminActor(string id) =>
        new(AccessSubjectKind.AdminActor, id);

    public static AccessSubject Service(string id) =>
        new(AccessSubjectKind.Service, id);

    public static AccessSubject System(string id) =>
        new(AccessSubjectKind.System, id);

    public static bool TryCreate(
        AccessSubjectKind kind,
        string? id,
        [NotNullWhen(true)] out AccessSubject? subject)
    {
        subject = null;

        if (kind == AccessSubjectKind.Unknown || !Enum.IsDefined(kind) ||
            !AccessText.TryNormalizeIdentifier(id, IdMaxLength, out string? normalizedId))
        {
            return false;
        }

        subject = new AccessSubject(kind, normalizedId);
        return true;
    }
}
