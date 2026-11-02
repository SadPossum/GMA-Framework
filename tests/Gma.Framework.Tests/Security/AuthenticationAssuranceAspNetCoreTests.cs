namespace Gma.Framework.Tests.Security;

using System.Globalization;
using System.Security.Claims;
using Gma.Framework.Security;
using Gma.Framework.Security.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AuthenticationAssuranceAspNetCoreTests
{
    private static readonly DateTimeOffset NowUtc = new(2026, 7, 19, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Continues_when_context_and_authentication_age_are_satisfied()
    {
        AuthenticationAssuranceRequirement requirement = new(
            ["urn:test:acr:password"],
            TimeSpan.FromMinutes(10));
        DefaultHttpContext context = CreateContext(
            requirement,
            new Claim(ApplicationClaimNames.AuthenticationContextReference, "urn:test:acr:password"),
            AuthenticationTimeClaim(NowUtc.AddMinutes(-5)));

        object? result = await InvokeAsync(context);

        Ok<string> ok = Assert.IsType<Ok<string>>(result);
        Assert.Equal("next", ok.Value);
        Assert.False(context.Response.Headers.ContainsKey("WWW-Authenticate"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-timestamp")]
    public async Task Challenges_when_authentication_time_is_missing_or_malformed(string? authenticationTime)
    {
        AuthenticationAssuranceRequirement requirement = new(maxAuthenticationAge: TimeSpan.FromMinutes(10));
        List<Claim> claims = authenticationTime is null
            ? []
            : [new Claim(ApplicationClaimNames.AuthenticationTime, authenticationTime)];
        DefaultHttpContext context = CreateContext(requirement, [.. claims]);

        object? result = await InvokeAsync(context);

        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
        Assert.Equal(AuthenticationAssuranceHttpErrorCodes.InsufficientAuthentication, problem.ProblemDetails.Title);
        Assert.Contains("insufficient_user_authentication", context.Response.Headers.WWWAuthenticate.ToString());
    }

    [Fact]
    public async Task Challenges_future_or_stale_authentication_events()
    {
        AuthenticationAssuranceRequirement requirement = new(maxAuthenticationAge: TimeSpan.FromMinutes(10));

        foreach (DateTimeOffset authenticatedAtUtc in new[] { NowUtc.AddSeconds(1), NowUtc.AddMinutes(-11) })
        {
            DefaultHttpContext context = CreateContext(requirement, AuthenticationTimeClaim(authenticatedAtUtc));
            Assert.IsType<ProblemHttpResult>(await InvokeAsync(context));
        }
    }

    [Fact]
    public async Task Challenges_an_unaccepted_context_with_rfc_9470_parameters()
    {
        AuthenticationAssuranceRequirement requirement = new(
            ["urn:test:acr:password", "urn:test:acr:mfa"],
            TimeSpan.FromSeconds(90));
        DefaultHttpContext context = CreateContext(
            requirement,
            new Claim(ApplicationClaimNames.AuthenticationContextReference, "urn:test:acr:external"),
            AuthenticationTimeClaim(NowUtc));

        Assert.IsType<ProblemHttpResult>(await InvokeAsync(context));

        string challenge = context.Response.Headers.WWWAuthenticate.ToString();
        Assert.Contains("error=\"insufficient_user_authentication\"", challenge);
        Assert.Contains("acr_values=\"urn:test:acr:password urn:test:acr:mfa\"", challenge);
        Assert.Contains("max_age=\"90\"", challenge);
        Assert.Equal(challenge, AuthenticationAssuranceChallenge.Create(requirement));
    }

    [Fact]
    public async Task Challenges_an_unauthenticated_principal()
    {
        AuthenticationAssuranceRequirement requirement = new(["urn:test:acr:password"]);
        DefaultHttpContext context = CreateContext(requirement, authenticated: false);

        Assert.IsType<ProblemHttpResult>(await InvokeAsync(context));
    }

    [Fact]
    public async Task Layered_route_requirements_compose_and_cannot_be_weakened()
    {
        CountingTimeProvider timeProvider = new(NowUtc);
        bool actionCalled = false;
        await using WebApplication application = CreateApplication(timeProvider);
        AuthenticationAssuranceRequirement outerRequirement = new(
            ["urn:test:acr:mfa"],
            TimeSpan.FromMinutes(5));
        AuthenticationAssuranceRequirement nestedRequirement = new(
            maxAuthenticationAge: TimeSpan.FromMinutes(10));
        AuthenticationAssuranceRequirement endpointRequirement = new(
            ["urn:test:acr:password", "urn:test:acr:mfa"],
            TimeSpan.FromMinutes(60));

        RouteGroupBuilder outerGroup = application
            .MapGroup("/api")
            .RequireAuthenticationAssurance(outerRequirement);
        RouteGroupBuilder nestedGroup = outerGroup
            .MapGroup("/staff")
            .RequireAuthenticationAssurance(nestedRequirement);
        nestedGroup
            .MapPost("/manage", () =>
            {
                actionCalled = true;
                return Results.NoContent();
            })
            .RequireAuthenticationAssurance(endpointRequirement);

        RouteEndpoint endpoint = GetSingleRouteEndpoint(application);
        IReadOnlyList<AuthenticationAssuranceMetadata> declaredRequirements =
            endpoint.Metadata.GetOrderedMetadata<AuthenticationAssuranceMetadata>();
        Assert.Collection(
            declaredRequirements,
            metadata => Assert.Same(outerRequirement, metadata.Requirement),
            metadata => Assert.Same(nestedRequirement, metadata.Requirement),
            metadata => Assert.Same(endpointRequirement, metadata.Requirement));
        EffectiveAuthenticationAssuranceMetadata effectiveMetadata = Assert.IsType<EffectiveAuthenticationAssuranceMetadata>(
            endpoint.Metadata.GetMetadata<EffectiveAuthenticationAssuranceMetadata>());
        Assert.Equal(["urn:test:acr:mfa"], effectiveMetadata.Requirement.AcceptedContextReferences);
        Assert.Equal(TimeSpan.FromMinutes(5), effectiveMetadata.Requirement.MaxAuthenticationAge);
        Assert.Single(endpoint.Metadata.GetOrderedMetadata<AuthenticationAssuranceFilterRegistrationMetadata>());
        Assert.NotEmpty(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>());

        DefaultHttpContext context = CreatePipelineContext(
            endpoint,
            application.Services,
            new Claim(ApplicationClaimNames.AuthenticationContextReference, "urn:test:acr:password"),
            AuthenticationTimeClaim(NowUtc.AddMinutes(-1)));

        await endpoint.RequestDelegate!(context);

        Assert.False(actionCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Equal(
            AuthenticationAssuranceChallenge.Create(effectiveMetadata.Requirement),
            context.Response.Headers.WWWAuthenticate.ToString());
        Assert.Equal(1, timeProvider.GetUtcNowCallCount);
    }

    [Fact]
    public async Task Layered_route_requirements_invoke_the_action_once_when_all_are_satisfied()
    {
        CountingTimeProvider timeProvider = new(NowUtc);
        int actionCallCount = 0;
        await using WebApplication application = CreateApplication(timeProvider);
        RouteGroupBuilder group = application
            .MapGroup("/api")
            .RequireAuthenticationAssurance(new AuthenticationAssuranceRequirement(
                ["urn:test:acr:password", "urn:test:acr:mfa"],
                TimeSpan.FromMinutes(30)));
        group
            .MapPost("/staff/manage", () =>
            {
                actionCallCount++;
                return Results.NoContent();
            })
            .RequireAuthenticationAssurance(new AuthenticationAssuranceRequirement(
                ["urn:test:acr:mfa"],
                TimeSpan.FromMinutes(5)));
        RouteEndpoint endpoint = GetSingleRouteEndpoint(application);
        DefaultHttpContext context = CreatePipelineContext(
            endpoint,
            application.Services,
            new Claim(ApplicationClaimNames.AuthenticationContextReference, "urn:test:acr:mfa"),
            AuthenticationTimeClaim(NowUtc.AddMinutes(-4)));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(1, actionCallCount);
        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
        Assert.Equal(1, timeProvider.GetUtcNowCallCount);
    }

    [Fact]
    public async Task Group_requirement_added_after_route_mapping_still_composes_with_endpoint_requirement()
    {
        CountingTimeProvider timeProvider = new(NowUtc);
        await using WebApplication application = CreateApplication(timeProvider);
        RouteGroupBuilder group = application.MapGroup("/api");
        group
            .MapGet("/sensitive", () => Results.NoContent())
            .RequireAuthenticationAssurance(new AuthenticationAssuranceRequirement(
                ["urn:test:acr:password", "urn:test:acr:mfa"],
                TimeSpan.FromMinutes(30)));
        group.RequireAuthenticationAssurance(new AuthenticationAssuranceRequirement(
            ["urn:test:acr:mfa"],
            TimeSpan.FromMinutes(5)));
        RouteEndpoint endpoint = GetSingleRouteEndpoint(application);
        EffectiveAuthenticationAssuranceMetadata effectiveMetadata = Assert.IsType<EffectiveAuthenticationAssuranceMetadata>(
            endpoint.Metadata.GetMetadata<EffectiveAuthenticationAssuranceMetadata>());
        DefaultHttpContext context = CreatePipelineContext(
            endpoint,
            application.Services,
            new Claim(ApplicationClaimNames.AuthenticationContextReference, "urn:test:acr:password"),
            AuthenticationTimeClaim(NowUtc));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(["urn:test:acr:mfa"], effectiveMetadata.Requirement.AcceptedContextReferences);
        Assert.Equal(TimeSpan.FromMinutes(5), effectiveMetadata.Requirement.MaxAuthenticationAge);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Equal(1, timeProvider.GetUtcNowCallCount);
    }

    [Fact]
    public async Task Routing_and_authorization_pipeline_enforces_the_effective_requirement_per_request()
    {
        CountingTimeProvider timeProvider = new(NowUtc);
        int actionCallCount = 0;
        WebApplicationBuilder hostBuilder = WebApplication.CreateBuilder();
        hostBuilder.Services.AddAuthentication();
        hostBuilder.Services.AddAuthorization();
        hostBuilder.Services.AddSingleton<TimeProvider>(timeProvider);
        await using WebApplication host = hostBuilder.Build();
        ApplicationBuilder pipelineBuilder = new(host.Services);
        pipelineBuilder.UseRouting();
        pipelineBuilder.UseAuthentication();
        pipelineBuilder.UseAuthorization();
        pipelineBuilder.UseEndpoints(endpoints =>
        {
            RouteGroupBuilder group = endpoints
                .MapGroup("/api")
                .RequireAuthenticationAssurance(new AuthenticationAssuranceRequirement(
                    ["urn:test:acr:mfa"],
                    TimeSpan.FromMinutes(5)));
            group
                .MapGet("/manage", () =>
                {
                    actionCallCount++;
                    return Results.NoContent();
                })
                .RequireAuthenticationAssurance(new AuthenticationAssuranceRequirement(
                    ["urn:test:acr:password", "urn:test:acr:mfa"],
                    TimeSpan.FromMinutes(30)));
        });
        RequestDelegate pipeline = pipelineBuilder.Build();
        await using AsyncServiceScope weakScope = host.Services.CreateAsyncScope();
        DefaultHttpContext weakContext = CreateRequestContext(
            weakScope.ServiceProvider,
            "/api/manage",
            new Claim(ApplicationClaimNames.AuthenticationContextReference, "urn:test:acr:password"),
            AuthenticationTimeClaim(NowUtc));

        await pipeline(weakContext);

        Assert.Equal(StatusCodes.Status401Unauthorized, weakContext.Response.StatusCode);
        Assert.Contains("acr_values=\"urn:test:acr:mfa\"", weakContext.Response.Headers.WWWAuthenticate.ToString());
        Assert.Contains("max_age=\"300\"", weakContext.Response.Headers.WWWAuthenticate.ToString());
        Assert.Equal(0, actionCallCount);
        RouteEndpoint routedEndpoint = Assert.IsType<RouteEndpoint>(weakContext.GetEndpoint());
        Assert.Equal(2, routedEndpoint.Metadata.GetOrderedMetadata<AuthenticationAssuranceMetadata>().Count);
        Assert.Equal(2, routedEndpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count);
        Assert.Single(routedEndpoint.Metadata.GetOrderedMetadata<AuthenticationAssuranceFilterRegistrationMetadata>());

        await using AsyncServiceScope strongScope = host.Services.CreateAsyncScope();
        DefaultHttpContext strongContext = CreateRequestContext(
            strongScope.ServiceProvider,
            "/api/manage",
            new Claim(ApplicationClaimNames.AuthenticationContextReference, "urn:test:acr:mfa"),
            AuthenticationTimeClaim(NowUtc.AddMinutes(-4)));

        await pipeline(strongContext);

        Assert.Equal(StatusCodes.Status204NoContent, strongContext.Response.StatusCode);
        Assert.Equal(1, actionCallCount);
        Assert.Equal(2, timeProvider.GetUtcNowCallCount);
    }

    [Fact]
    public async Task Repeated_endpoint_declarations_keep_every_requirement_but_register_one_filter()
    {
        CountingTimeProvider timeProvider = new(NowUtc);
        await using WebApplication application = CreateApplication(timeProvider);
        RouteHandlerBuilder route = application.MapGet("/sensitive", () => Results.NoContent());
        route.RequireAuthenticationAssurance(new AuthenticationAssuranceRequirement(
            ["urn:test:acr:password", "urn:test:acr:mfa"]));
        route.RequireAuthenticationAssurance(new AuthenticationAssuranceRequirement(
            maxAuthenticationAge: TimeSpan.FromMinutes(5)));
        route.RequireAuthenticationAssurance(new AuthenticationAssuranceRequirement(
            ["urn:test:acr:mfa", "urn:test:acr:password"]));
        RouteEndpoint endpoint = GetSingleRouteEndpoint(application);

        Assert.Equal(3, endpoint.Metadata.GetOrderedMetadata<AuthenticationAssuranceMetadata>().Count);
        Assert.Single(endpoint.Metadata.GetOrderedMetadata<AuthenticationAssuranceFilterRegistrationMetadata>());
        EffectiveAuthenticationAssuranceMetadata effectiveMetadata = Assert.IsType<EffectiveAuthenticationAssuranceMetadata>(
            endpoint.Metadata.GetMetadata<EffectiveAuthenticationAssuranceMetadata>());
        Assert.Equal(["urn:test:acr:mfa", "urn:test:acr:password"], effectiveMetadata.Requirement.AcceptedContextReferences);
        Assert.Equal(TimeSpan.FromMinutes(5), effectiveMetadata.Requirement.MaxAuthenticationAge);
        DefaultHttpContext context = CreatePipelineContext(
            endpoint,
            application.Services,
            new Claim(ApplicationClaimNames.AuthenticationContextReference, "urn:test:acr:mfa"),
            AuthenticationTimeClaim(NowUtc));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
        Assert.Equal(1, timeProvider.GetUtcNowCallCount);
    }

    [Theory]
    [InlineData("urn:test:acr:password", "urn:test:acr:mfa")]
    [InlineData("urn:test:acr:mfa", "urn:test:acr:MFA")]
    public void Disjoint_layered_context_requirements_fail_during_endpoint_construction(
        string outerContext,
        string endpointContext)
    {
        using WebApplication application = WebApplication.Create();
        RouteGroupBuilder group = application
            .MapGroup("/api")
            .RequireAuthenticationAssurance(new AuthenticationAssuranceRequirement(
                [outerContext]));
        group
            .MapGet("/mfa-only", () => Results.NoContent())
            .RequireAuthenticationAssurance(new AuthenticationAssuranceRequirement(
                [endpointContext]));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => GetSingleRouteEndpoint(application));

        Assert.Contains("no accepted authentication context in common", exception.Message);
    }

    [Fact]
    public void Effective_requirement_uses_endpoint_context_preference_and_the_shortest_age()
    {
        AuthenticationAssuranceRequirement effective = AuthenticationAssuranceRequirementComposer.Compose(
        [
            new AuthenticationAssuranceRequirement(
                ["urn:test:acr:password", "urn:test:acr:mfa", "urn:test:acr:hardware"],
                TimeSpan.FromMinutes(30)),
            new AuthenticationAssuranceRequirement(
                ["urn:test:acr:hardware", "urn:test:acr:mfa"],
                TimeSpan.FromMinutes(10)),
            new AuthenticationAssuranceRequirement(
                ["urn:test:acr:mfa", "urn:test:acr:hardware"],
                TimeSpan.FromMinutes(15))
        ]);

        Assert.Equal(
            ["urn:test:acr:mfa", "urn:test:acr:hardware"],
            effective.AcceptedContextReferences);
        Assert.Equal(TimeSpan.FromMinutes(10), effective.MaxAuthenticationAge);
    }

    private static async Task<object?> InvokeAsync(DefaultHttpContext context)
    {
        AuthenticationAssuranceEndpointFilter filter = new();
        return await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(context),
            _ => ValueTask.FromResult<object?>(Results.Ok("next")));
    }

    private static DefaultHttpContext CreateContext(
        AuthenticationAssuranceRequirement requirement,
        params Claim[] claims) =>
        CreateContext(requirement, true, claims);

    private static DefaultHttpContext CreateContext(
        AuthenticationAssuranceRequirement requirement,
        bool authenticated,
        params Claim[] claims)
    {
        ServiceCollection services = new();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(NowUtc));

        DefaultHttpContext context = new()
        {
            RequestServices = services.BuildServiceProvider()
        };
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AuthenticationAssuranceMetadata(requirement)),
            "test"));
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "test" : null));
        return context;
    }

    private static RouteEndpoint GetSingleRouteEndpoint(WebApplication application) =>
        Assert.IsType<RouteEndpoint>(Assert.Single(
            ((IEndpointRouteBuilder)application).DataSources.SelectMany(source => source.Endpoints)));

    private static WebApplication CreateApplication(TimeProvider timeProvider)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(timeProvider);
        return builder.Build();
    }

    private static DefaultHttpContext CreatePipelineContext(
        Endpoint endpoint,
        IServiceProvider services,
        params Claim[] claims)
    {
        DefaultHttpContext context = new()
        {
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
        };
        context.Response.Body = new MemoryStream();
        context.SetEndpoint(endpoint);
        return context;
    }

    private static DefaultHttpContext CreateRequestContext(
        IServiceProvider services,
        string path,
        params Claim[] claims)
    {
        DefaultHttpContext context = new()
        {
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
        };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static Claim AuthenticationTimeClaim(DateTimeOffset value) =>
        new(
            ApplicationClaimNames.AuthenticationTime,
            value.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class CountingTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public int GetUtcNowCallCount { get; private set; }

        public override DateTimeOffset GetUtcNow()
        {
            this.GetUtcNowCallCount++;
            return utcNow;
        }
    }
}
