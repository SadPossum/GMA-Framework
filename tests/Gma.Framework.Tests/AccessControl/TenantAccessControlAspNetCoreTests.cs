namespace Gma.Framework.Tests.AccessControl;

using System.Security.Claims;
using Gma.Framework.AccessControl;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Permissions;
using Gma.Framework.Security;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.AccessControl.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TenantAccessControlAspNetCoreTests
{
    [Fact]
    public async Task Tenant_bridge_resolves_access_scope_from_tenant_context()
    {
        RecordingAuthorizationService authorization = new(AccessDecision.Allowed());
        DefaultHttpContext httpContext = CreateHttpContext(authorization, new TestTenantContext("tenant-a"));
        AccessPermissionEndpointFilter filter = new();

        object? result = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(httpContext),
            _ => ValueTask.FromResult<object?>(Results.Ok("next")));

        Ok<string> ok = Assert.IsType<Ok<string>>(result);
        Assert.Equal("next", ok.Value);
        AccessRequirement requirement = Assert.Single(authorization.Requirements);
        Assert.Equal("catalog.items.read", requirement.Permission.Value);
        Assert.Equal("tenant:tenant-a", requirement.Scope.Value);
        Assert.Equal("user-1", requirement.Subject.Id);
    }

    [Fact]
    public async Task Tenant_bridge_rejects_missing_tenant_context_before_authorization()
    {
        RecordingAuthorizationService authorization = new(AccessDecision.Allowed());
        DefaultHttpContext httpContext = CreateHttpContext(authorization, new TestTenantContext(null));
        AccessPermissionEndpointFilter filter = new();

        object? result = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(httpContext),
            _ => ValueTask.FromResult<object?>(Results.Ok("next")));

        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal(AccessControlHttpErrorCodes.ScopeRequired, problem.ProblemDetails.Title);
        Assert.Equal(0, authorization.CallCount);
    }

    private static DefaultHttpContext CreateHttpContext(
        RecordingAuthorizationService authorization,
        ITenantContext tenantContext)
    {
        ServiceCollection services = new();
        services.AddSingleton<IAccessAuthorizationService>(authorization);
        services.AddSingleton(tenantContext);
        services.AddGmaTenantAccessControlAspNetCore();

        DefaultHttpContext httpContext = new()
        {
            RequestServices = services.BuildServiceProvider()
        };
        httpContext.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AccessPermissionMetadata(
                PermissionCode.Create("catalog.items.read"),
                scopeResolverName: "tenant",
                requireScope: true)),
            "test"));
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(GmaClaimNames.Subject, "user-1")],
            "test"));

        return httpContext;
    }

    private sealed class TestTenantContext(string? scopeId) : ITenantContext
    {
        public bool IsEnabled => true;
        public string? TenantId { get; } = scopeId;
    }

    private sealed class RecordingAuthorizationService(AccessDecision decision) : IAccessAuthorizationService
    {
        public int CallCount { get; private set; }
        public List<AccessRequirement> Requirements { get; } = [];

        public Task<AccessDecision> AuthorizeAsync(
            AccessRequirement requirement,
            CancellationToken cancellationToken)
        {
            this.CallCount++;
            this.Requirements.Add(requirement);
            return Task.FromResult(decision);
        }
    }
}
