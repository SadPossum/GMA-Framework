namespace Gma.Framework.Tests.Security;

using System.Globalization;
using System.Security.Claims;
using Gma.Framework.Security;
using Gma.Framework.Security.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
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
    }

    [Fact]
    public async Task Challenges_an_unauthenticated_principal()
    {
        AuthenticationAssuranceRequirement requirement = new(["urn:test:acr:password"]);
        DefaultHttpContext context = CreateContext(requirement, authenticated: false);

        Assert.IsType<ProblemHttpResult>(await InvokeAsync(context));
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

    private static Claim AuthenticationTimeClaim(DateTimeOffset value) =>
        new(
            ApplicationClaimNames.AuthenticationTime,
            value.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
