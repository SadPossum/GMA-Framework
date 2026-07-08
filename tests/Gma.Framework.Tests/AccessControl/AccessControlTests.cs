namespace Gma.Framework.Tests.AccessControl;

using Gma.Framework.AccessControl;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AccessControlTests
{
    [Fact]
    public void Access_subject_normalizes_known_kinds_and_tenant_ids()
    {
        AccessSubject subject = AccessSubject.User(" user-1 ", " tenant-a ");

        Assert.Equal(AccessSubjectKind.User, subject.Kind);
        Assert.Equal("user-1", subject.Id);
        Assert.Equal("tenant-a", subject.TenantId);
    }

    [Fact]
    public void Access_subject_factories_cover_admin_service_and_system_callers()
    {
        AccessSubject admin = AccessSubject.AdminActor(" admin-1 ", " tenant-a ");
        AccessSubject service = AccessSubject.Service(" notifications-worker ");
        AccessSubject system = AccessSubject.System(" platform ");

        Assert.Equal(AccessSubjectKind.AdminActor, admin.Kind);
        Assert.Equal("admin-1", admin.Id);
        Assert.Equal("tenant-a", admin.TenantId);
        Assert.Equal(AccessSubjectKind.Service, service.Kind);
        Assert.Equal("notifications-worker", service.Id);
        Assert.Null(service.TenantId);
        Assert.Equal(AccessSubjectKind.System, system.Kind);
        Assert.Equal("platform", system.Id);
        Assert.Null(system.TenantId);
    }

    [Fact]
    public void Access_subject_rejects_unknown_kind_empty_id_and_invalid_tenant()
    {
        Assert.Throws<ArgumentException>(() => new AccessSubject(AccessSubjectKind.Unknown, "user-1", "tenant-a"));
        Assert.Throws<ArgumentException>(() => AccessSubject.User(" ", "tenant-a"));
        Assert.Throws<ArgumentException>(() => AccessSubject.User("user-1", "tenant a"));

        Assert.False(AccessSubject.TryCreate(AccessSubjectKind.User, " ", "tenant-a", out _));
        Assert.False(AccessSubject.TryCreate((AccessSubjectKind)999, "user-1", "tenant-a", out _));
        Assert.False(AccessSubject.TryCreate(AccessSubjectKind.User, "user-1", "tenant a", out _));
    }

}
