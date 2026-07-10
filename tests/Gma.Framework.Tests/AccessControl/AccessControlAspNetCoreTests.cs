namespace Gma.Framework.Tests.AccessControl;

using System.Security.Claims;
using Gma.Framework.AccessControl;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Permissions;
using Gma.Framework.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AccessControlAspNetCoreTests
{
    [Fact]
    public async Task Permission_filter_returns_unauthenticated_when_subject_is_missing()
    {
        HttpContext httpContext = CreateHttpContext(AccessDecision.Allowed(), authenticated: false);
        AccessPermissionEndpointFilter filter = new();

        object? result = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(httpContext),
            _ => ValueTask.FromResult<object?>(Results.Ok("next")));

        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
        Assert.Equal(AccessControlHttpErrorCodes.Unauthenticated, problem.ProblemDetails.Title);
    }

    [Fact]
    public async Task Permission_filter_returns_forbidden_when_authorization_denies()
    {
        RecordingAuthorizationService authorization = new(AccessDecision.Denied("access.denied", "Nope."));
        HttpContext httpContext = CreateHttpContext(authorization);
        AccessPermissionEndpointFilter filter = new();

        object? result = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(httpContext),
            _ => ValueTask.FromResult<object?>(Results.Ok("next")));

        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
        Assert.Equal(AccessControlHttpErrorCodes.Unauthorized, problem.ProblemDetails.Title);
        Assert.Equal("Nope.", problem.ProblemDetails.Detail);
        Assert.Equal(1, authorization.CallCount);
    }

    [Fact]
    public async Task Permission_filter_passes_global_scope_to_authorization_and_continues_when_allowed()
    {
        RecordingAuthorizationService authorization = new(AccessDecision.Allowed());
        HttpContext httpContext = CreateHttpContext(authorization);
        AccessPermissionEndpointFilter filter = new();

        object? result = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(httpContext),
            _ => ValueTask.FromResult<object?>(Results.Ok("next")));

        Ok<string> ok = Assert.IsType<Ok<string>>(result);
        Assert.Equal("next", ok.Value);
        AccessRequirement requirement = Assert.Single(authorization.Requirements);
        Assert.Equal("auth.members.read", requirement.Permission.Value);
        Assert.Equal("global", requirement.Scope.Value);
        Assert.Equal("user-1", requirement.Subject.Id);
    }

    [Fact]
    public async Task Permission_filter_passes_route_scope_to_authorization()
    {
        RecordingAuthorizationService authorization = new(AccessDecision.Allowed());
        HttpContext httpContext = CreateHttpContext(
            authorization,
            metadata: new AccessPermissionMetadata(
                PermissionCode.Create("catalog.items.read"),
                scopeSegmentName: "property",
                scopeRouteValueName: "propertyId",
                requireScope: true));
        httpContext.Request.RouteValues["propertyId"] = "property-1";
        AccessPermissionEndpointFilter filter = new();

        object? result = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(httpContext),
            _ => ValueTask.FromResult<object?>(Results.Ok("next")));

        Ok<string> ok = Assert.IsType<Ok<string>>(result);
        Assert.Equal("next", ok.Value);
        AccessRequirement requirement = Assert.Single(authorization.Requirements);
        Assert.Equal("catalog.items.read", requirement.Permission.Value);
        Assert.Equal("property:property-1", requirement.Scope.Value);
    }

    [Fact]
    public async Task Required_scope_returns_bad_request_when_scope_is_missing()
    {
        RecordingAuthorizationService authorization = new(AccessDecision.Allowed());
        HttpContext httpContext = CreateHttpContext(
            authorization,
            metadata: new AccessPermissionMetadata(
                PermissionCode.Create("auth.members.read"),
                requireScope: true));
        AccessPermissionEndpointFilter filter = new();

        object? result = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(httpContext),
            _ => ValueTask.FromResult<object?>(Results.Ok("next")));

        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal(AccessControlHttpErrorCodes.ScopeRequired, problem.ProblemDetails.Title);
        Assert.Equal(0, authorization.CallCount);
    }

    [Fact]
    public async Task Unknown_scope_resolver_returns_server_error_before_authorization()
    {
        RecordingAuthorizationService authorization = new(AccessDecision.Allowed());
        HttpContext httpContext = CreateHttpContext(
            authorization,
            metadata: new AccessPermissionMetadata(
                PermissionCode.Create("auth.members.read"),
                scopeResolverName: "missing",
                requireScope: true));
        AccessPermissionEndpointFilter filter = new();

        object? result = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(httpContext),
            _ => ValueTask.FromResult<object?>(Results.Ok("next")));

        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
        Assert.Equal(AccessControlHttpErrorCodes.ScopeResolverMissing, problem.ProblemDetails.Title);
        Assert.Equal(0, authorization.CallCount);
    }

    private static DefaultHttpContext CreateHttpContext(
        AccessDecision decision,
        bool authenticated = true) =>
        CreateHttpContext(new RecordingAuthorizationService(decision), authenticated: authenticated);

    private static DefaultHttpContext CreateHttpContext(
        RecordingAuthorizationService authorization,
        AccessPermissionMetadata? metadata = null,
        bool authenticated = true)
    {
        metadata ??= new AccessPermissionMetadata(PermissionCode.Create("auth.members.read"));
        ServiceCollection services = new();
        services.AddSingleton<IAccessAuthorizationService>(authorization);
        services.AddGmaAccessControlAspNetCore();

        DefaultHttpContext httpContext = new()
        {
            RequestServices = services.BuildServiceProvider()
        };
        httpContext.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(metadata),
            "test"));

        if (authenticated)
        {
            List<Claim> claims = [new(GmaClaimNames.Subject, "user-1")];
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        }

        return httpContext;
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
