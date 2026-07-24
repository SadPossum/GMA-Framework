namespace Gma.Framework.Tests.AccessControl;

using System.Security.Claims;
using Gma.Framework.AccessControl;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Permissions;
using Gma.Framework.Security;
using Gma.Framework.Tenancy.AccessControl.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AccessPermissionSetAspNetCoreTests
{
    [Fact]
    public async Task Permission_set_requires_every_permission_in_its_own_scope()
    {
        RecordingAuthorizationService authorization = new();
        AccessPermissionMetadata[] requirements =
        [
            new(
                PermissionCode.Create("data.erase"),
                AccessScope.Parse("tenant:tenant-a"),
                requireScope: true),
            new(
                PermissionCode.Create("properties.read"),
                AccessScope.Parse("tenant:tenant-a/property:property-1"),
                requireScope: true)
        ];
        HttpContext httpContext = CreateHttpContext(
            authorization,
            new AccessPermissionSetMetadata(requirements));
        AccessPermissionSetEndpointFilter filter = new();

        object? result = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(httpContext),
            _ => ValueTask.FromResult<object?>(Results.Ok("next")));

        Assert.Equal("next", Assert.IsType<Ok<string>>(result).Value);
        Assert.Collection(
            authorization.Requirements,
            requirement =>
            {
                Assert.Equal("data.erase", requirement.Permission.Value);
                Assert.Equal("tenant:tenant-a", requirement.Scope.Value);
            },
            requirement =>
            {
                Assert.Equal("properties.read", requirement.Permission.Value);
                Assert.Equal(
                    "tenant:tenant-a/property:property-1",
                    requirement.Scope.Value);
            });
    }

    [Fact]
    public async Task Permission_set_stops_before_the_action_when_any_requirement_is_denied()
    {
        RecordingAuthorizationService authorization = new(denyCall: 2);
        AccessPermissionSetMetadata metadata = new(
        [
            new(PermissionCode.Create("data.erase")),
            new(PermissionCode.Create("properties.read"))
        ]);
        HttpContext httpContext = CreateHttpContext(authorization, metadata);
        AccessPermissionSetEndpointFilter filter = new();
        bool actionCalled = false;

        object? result = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(httpContext),
            _ =>
            {
                actionCalled = true;
                return ValueTask.FromResult<object?>(Results.Ok());
            });

        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
        Assert.False(actionCalled);
        Assert.Equal(2, authorization.Requirements.Count);
    }

    [Fact]
    public void Permission_set_rejects_empty_and_duplicate_requirements()
    {
        Assert.Throws<ArgumentException>(() => new AccessPermissionSetMetadata([]));

        AccessPermissionMetadata requirement =
            new(PermissionCode.Create("data.erase"));
        Assert.Throws<ArgumentException>(() =>
            new AccessPermissionSetMetadata([requirement, requirement]));
    }

    [Fact]
    public void Tenant_permission_metadata_uses_the_registered_tenant_scope_resolver()
    {
        AccessPermissionMetadata metadata =
            TenantAccessPermissionMetadata.Create("data.erase");

        Assert.Equal("data.erase", metadata.Permission.Value);
        Assert.Equal("tenant", metadata.ScopeResolverName);
        Assert.True(metadata.RequireScope);
    }

    private static DefaultHttpContext CreateHttpContext(
        IAccessAuthorizationService authorization,
        AccessPermissionSetMetadata metadata)
    {
        ServiceCollection services = new();
        services.AddSingleton(authorization);
        services.AddGmaAccessControlAspNetCore();

        DefaultHttpContext httpContext = new()
        {
            RequestServices = services.BuildServiceProvider(),
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(GmaClaimNames.Subject, "user-1")],
                "test"))
        };
        httpContext.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(metadata),
            "test"));
        return httpContext;
    }

    private sealed class RecordingAuthorizationService(int? denyCall = null)
        : IAccessAuthorizationService
    {
        public List<AccessRequirement> Requirements { get; } = [];

        public Task<AccessDecision> AuthorizeAsync(
            AccessRequirement requirement,
            CancellationToken cancellationToken)
        {
            this.Requirements.Add(requirement);
            return Task.FromResult(
                this.Requirements.Count == denyCall
                    ? AccessDecision.Denied("access.denied", "Denied.")
                    : AccessDecision.Allowed());
        }
    }
}
