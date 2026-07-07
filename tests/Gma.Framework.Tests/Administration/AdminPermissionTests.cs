namespace Gma.Framework.Tests;

using Gma.Framework.Administration;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdminPermissionTests
{
    [Fact]
    public void Create_normalizes_permission_code()
    {
        AdminPermission permission = AdminPermission.Create("Auth.Members.Read");

        Assert.Equal("auth.members.read", permission.Code);
    }

    [Fact]
    public void Create_rejects_invalid_permission_code()
    {
        Assert.Throws<ArgumentException>(() => AdminPermission.Create("auth"));
    }

    [Fact]
    public void Create_rejects_overlong_permission_code()
    {
        string code = $"auth.{new string('x', AdminPermission.MaxLength)}";

        Assert.Throws<ArgumentException>(() => AdminPermission.Create(code));
    }

    [Fact]
    public void Try_create_returns_false_instead_of_throwing_for_invalid_code()
    {
        Assert.False(AdminPermission.TryCreate("auth", out AdminPermission? invalid));
        Assert.False(AdminPermission.TryCreate($"auth.{new string('x', AdminPermission.MaxLength)}", out AdminPermission? overlong));
        Assert.Null(invalid);
        Assert.Null(overlong);

        Assert.True(AdminPermission.TryCreate("Auth.Members.Read", out AdminPermission? valid));
        Assert.Equal("auth.members.read", valid.Code);
    }

    [Fact]
    public async Task Deny_all_authorization_denies_by_default()
    {
        DenyAllAdminAuthorizationService authorization = new();

        AdminAuthorizationResult result = await authorization.AuthorizeAsync(
            AdminActor.System("actor"),
            AdminPermission.Create("auth.members.read"),
            "tenant-a",
            CancellationToken.None);

        Assert.False(result.IsAuthorized);
    }
}
