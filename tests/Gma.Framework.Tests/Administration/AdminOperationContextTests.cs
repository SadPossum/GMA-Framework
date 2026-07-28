namespace Gma.Framework.Tests;

using Gma.Framework.Administration;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdminOperationContextTests
{
    private static readonly AdminActor Actor = AdminActor.System("actor");
    private static readonly AdminOperation Operation = AdminOperation.Create(
        "auth.members.read",
        AdminPermission.Create("auth.members.read"));

    [Fact]
    public void Constructor_assigns_values()
    {
        AdminResourceScope resourceScope = AdminResourceScope.Create(
            AdminResourceScopeSegment.Create("property", "property-a"));
        AdminOperationContext context = new(
            Actor,
            Operation,
            " tenant-a ",
            RequireTenant: true,
            AdminErrors.TenantClaimMismatch,
            resourceScope);

        Assert.Equal(Actor, context.Actor);
        Assert.Equal(Operation, context.Operation);
        Assert.Equal(" tenant-a ", context.TenantId);
        Assert.True(context.RequireTenant);
        Assert.Equal(AdminErrors.TenantClaimMismatch, context.PreAuthorizationError);
        Assert.Same(resourceScope, context.ResourceScope);
    }

    [Fact]
    public void Constructor_rejects_missing_actor()
    {
        Assert.Throws<ArgumentNullException>(() => new AdminOperationContext(
            null!,
            Operation,
            "tenant-a",
            RequireTenant: true));
    }

    [Fact]
    public void Constructor_rejects_missing_operation()
    {
        Assert.Throws<ArgumentNullException>(() => new AdminOperationContext(
            Actor,
            null!,
            "tenant-a",
            RequireTenant: true));
    }
}
