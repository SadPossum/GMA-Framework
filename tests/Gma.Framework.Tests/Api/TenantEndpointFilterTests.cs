namespace Gma.Framework.Tests;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Gma.Framework.Api.Tenancy;
using Gma.Framework.Tenancy;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TenantEndpointFilterTests
{
    [Fact]
    public void Require_tenant_rejects_null_builder()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TenantEndpointRouteBuilderExtensions.RequireTenant(null!));
    }

    [Fact]
    public void Independently_authenticated_tenant_endpoint_rejects_null_builder()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TenantEndpointRouteBuilderExtensions.RequireTenantWithIndependentAuthentication(null!));
    }

    [Fact]
    public async Task Missing_tenant_header_returns_required_problem()
    {
        HttpContext httpContext = CreateHttpContext();
        TenantEndpointFilter filter = new();

        object? result = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(httpContext),
            _ => ValueTask.FromResult<object?>(Results.Ok()));

        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal(TenantErrors.TenantRequired.Code, problem.ProblemDetails.Title);
    }

    [Fact]
    public async Task Missing_tenant_header_clears_stale_context()
    {
        RecordingTenantContext tenantContext = new();
        tenantContext.SetTenant("stale-tenant");
        HttpContext httpContext = CreateHttpContext(tenantContext);
        TenantEndpointFilter filter = new();

        object? result = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(httpContext),
            _ => ValueTask.FromResult<object?>(Results.Ok()));

        Assert.IsType<ProblemHttpResult>(result);
        Assert.Null(tenantContext.TenantId);
    }

    [Fact]
    public async Task Multiple_tenant_header_values_return_invalid_problem()
    {
        HttpContext httpContext = CreateHttpContext();
        httpContext.Request.Headers["X-Tenant-Id"] = new StringValues(["tenant-a", "tenant-b"]);
        TenantEndpointFilter filter = new();

        object? result = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(httpContext),
            _ => ValueTask.FromResult<object?>(Results.Ok()));

        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal(TenantErrors.TenantInvalid.Code, problem.ProblemDetails.Title);
    }

    [Fact]
    public async Task Valid_tenant_header_sets_context_and_continues()
    {
        RecordingTenantContext tenantContext = new();
        HttpContext httpContext = CreateHttpContext(tenantContext);
        httpContext.Request.Headers["X-Tenant-Id"] = " tenant-a ";
        TenantEndpointFilter filter = new();

        object? result = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(httpContext),
            _ => ValueTask.FromResult<object?>(Results.Ok("next")));

        Ok<string> ok = Assert.IsType<Ok<string>>(result);
        Assert.Equal("next", ok.Value);
        Assert.Equal("tenant-a", tenantContext.TenantId);
    }

    [Fact]
    public async Task Registered_access_policies_run_after_tenant_resolution()
    {
        RecordingTenantContext tenantContext = new();
        RecordingTenantAccessPolicy accessPolicy = new(TenantEndpointAccessDecision.Allowed);
        HttpContext httpContext = CreateHttpContext(tenantContext, accessPolicy);
        httpContext.Request.Headers["X-Tenant-Id"] = "tenant-a";
        TenantEndpointFilter filter = new();

        object? result = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(httpContext),
            _ => ValueTask.FromResult<object?>(Results.Ok("next")));

        Assert.IsType<Ok<string>>(result);
        Assert.Equal("tenant-a", accessPolicy.TenantId);
        Assert.Same(httpContext, accessPolicy.HttpContext);
    }

    [Fact]
    public async Task Denied_access_policy_stops_the_endpoint_pipeline()
    {
        RecordingTenantAccessPolicy accessPolicy = new(
            TenantEndpointAccessDecision.Denied("TenantAccess.Denied", "Tenant access is denied."));
        HttpContext httpContext = CreateHttpContext(accessPolicy: accessPolicy);
        httpContext.Request.Headers["X-Tenant-Id"] = "tenant-a";
        TenantEndpointFilter filter = new();
        bool nextInvoked = false;

        object? result = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(httpContext),
            _ =>
            {
                nextInvoked = true;
                return ValueTask.FromResult<object?>(Results.Ok());
            });

        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
        Assert.Equal("TenantAccess.Denied", problem.ProblemDetails.Title);
        Assert.False(nextInvoked);
    }

    [Fact]
    public async Task Independently_authenticated_endpoint_skips_access_policies_after_tenant_resolution()
    {
        RecordingTenantContext tenantContext = new();
        RecordingTenantAccessPolicy accessPolicy = new(
            TenantEndpointAccessDecision.Denied("TenantAccess.Denied", "Tenant access is denied."));
        HttpContext httpContext = CreateHttpContext(tenantContext, accessPolicy);
        httpContext.Request.Headers["X-Tenant-Id"] = "tenant-a";
        httpContext.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(IndependentTenantEndpointAuthenticationMetadata.Instance),
            "independently-authenticated"));
        TenantEndpointFilter filter = new();

        object? result = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(httpContext),
            _ => ValueTask.FromResult<object?>(Results.Ok("next")));

        Ok<string> ok = Assert.IsType<Ok<string>>(result);
        Assert.Equal("next", ok.Value);
        Assert.Equal("tenant-a", tenantContext.TenantId);
        Assert.Null(accessPolicy.HttpContext);
    }

    private static DefaultHttpContext CreateHttpContext(
        RecordingTenantContext? tenantContext = null,
        ITenantEndpointAccessPolicy? accessPolicy = null)
    {
        tenantContext ??= new RecordingTenantContext();
        ServiceCollection services = new();
        services.AddSingleton<IOptions<TenantOptions>>(Options.Create(new TenantOptions { Enabled = true }));
        services.AddSingleton<ITenantContextAccessor>(tenantContext);
        services.AddSingleton<ITenantContext>(tenantContext);
        if (accessPolicy is not null)
        {
            services.AddSingleton(accessPolicy);
        }

        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };
    }

    private sealed class RecordingTenantContext : ITenantContextAccessor
    {
        public bool IsEnabled => true;
        public string? TenantId { get; private set; }
        public void SetTenant(string scopeId) => this.TenantId = scopeId;
        public void ClearTenant() => this.TenantId = null;
    }

    private sealed class RecordingTenantAccessPolicy(TenantEndpointAccessDecision decision)
        : ITenantEndpointAccessPolicy
    {
        public HttpContext? HttpContext { get; private set; }
        public string? TenantId { get; private set; }

        public ValueTask<TenantEndpointAccessDecision> AuthorizeAsync(
            HttpContext httpContext,
            string tenantId,
            CancellationToken cancellationToken)
        {
            this.HttpContext = httpContext;
            this.TenantId = tenantId;
            return ValueTask.FromResult(decision);
        }
    }
}
