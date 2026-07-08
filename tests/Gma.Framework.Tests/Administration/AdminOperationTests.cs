namespace Gma.Framework.Tests;

using Gma.Framework.Administration;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdminOperationTests
{
    private static readonly AdminPermission Permission = AdminPermission.Create("auth.members.read");

    [Fact]
    public void Create_normalizes_operation_name()
    {
        AdminOperation operation = AdminOperation.Create(" Auth.Members.Reset-Password ", Permission);

        Assert.Equal("auth.members.reset-password", operation.Name);
        Assert.Equal(Permission, operation.Permission);
    }

    [Theory]
    [InlineData("")]
    [InlineData("auth")]
    [InlineData("auth..members")]
    [InlineData("auth.members_")]
    [InlineData("auth.members.")]
    [InlineData("*")]
    public void Create_rejects_invalid_operation_names(string name)
    {
        Assert.Throws<ArgumentException>(() => AdminOperation.Create(name, Permission));
    }

    [Fact]
    public void Create_rejects_overlong_operation_name()
    {
        string name = $"auth.{new string('x', AdminOperation.MaxLength)}";

        Assert.Throws<ArgumentException>(() => AdminOperation.Create(name, Permission));
    }

    [Theory]
    [InlineData("")]
    [InlineData("auth")]
    [InlineData("auth..members")]
    public void Try_create_rejects_invalid_operation_names(string name)
    {
        Assert.False(AdminOperation.TryCreate(name, Permission, out AdminOperation? operation));
        Assert.Null(operation);
    }

    [Fact]
    public void Try_create_rejects_overlong_operation_name()
    {
        string name = $"auth.{new string('x', AdminOperation.MaxLength)}";

        Assert.False(AdminOperation.TryCreate(name, Permission, out AdminOperation? operation));
        Assert.Null(operation);
    }
}
