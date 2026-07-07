namespace Gma.Framework.AccessControl;

using System.Diagnostics.CodeAnalysis;
using Gma.Framework.Naming;

public sealed record AccessSubject
{
    public const int IdMaxLength = 256;

    public AccessSubject(AccessSubjectKind kind, string id, string? tenantId)
    {
        if (kind == AccessSubjectKind.Unknown || !Enum.IsDefined(kind))
        {
            throw new ArgumentException("Access subject kind must be a defined non-unknown value.", nameof(kind));
        }

        this.Kind = kind;
        this.Id = AccessText.NormalizeIdentifier(id, IdMaxLength, "Access subject id", nameof(id));
        this.TenantId = NormalizeTenantOrNull(tenantId);
    }

    public AccessSubjectKind Kind { get; }
    public string Id { get; }
    public string? TenantId { get; }

    public static AccessSubject User(string id, string? tenantId) =>
        new(AccessSubjectKind.User, id, tenantId);

    public static AccessSubject AdminActor(string id, string? tenantId) =>
        new(AccessSubjectKind.AdminActor, id, tenantId);

    public static AccessSubject Service(string id, string? tenantId = null) =>
        new(AccessSubjectKind.Service, id, tenantId);

    public static AccessSubject System(string id, string? tenantId = null) =>
        new(AccessSubjectKind.System, id, tenantId);

    public static bool TryCreate(
        AccessSubjectKind kind,
        string? id,
        string? tenantId,
        [NotNullWhen(true)] out AccessSubject? subject)
    {
        subject = null;

        if (kind == AccessSubjectKind.Unknown || !Enum.IsDefined(kind) ||
            !AccessText.TryNormalizeIdentifier(id, IdMaxLength, out string? normalizedId) ||
            !TryNormalizeTenantOrNull(tenantId, out string? normalizedTenantId))
        {
            return false;
        }

        subject = new AccessSubject(kind, normalizedId, normalizedTenantId);
        return true;
    }

    private static string? NormalizeTenantOrNull(string? tenantId)
    {
        if (TryNormalizeTenantOrNull(tenantId, out string? normalized))
        {
            return normalized;
        }

        throw new ArgumentException(
            $"Tenant id must be {TenantIds.MaxLength} characters or fewer and cannot contain whitespace or control characters.",
            nameof(tenantId));
    }

    private static bool TryNormalizeTenantOrNull(string? tenantId, out string? normalized)
    {
        normalized = null;
        if (tenantId is null)
        {
            return true;
        }

        return TenantIds.TryNormalize(tenantId, out normalized);
    }
}
