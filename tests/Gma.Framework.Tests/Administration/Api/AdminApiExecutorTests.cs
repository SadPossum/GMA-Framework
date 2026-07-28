namespace Gma.Framework.Tests;

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Api;
using Gma.Framework.Cqrs;
using Gma.Framework.Tenancy;
using Gma.Framework.Results;
using Gma.Framework.Security;
using Gma.Framework.Security.AspNetCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdminApiExecutorTests
{
    [Fact]
    public async Task Custom_success_callback_must_return_result()
    {
        AdminApiExecutor executor = CreateExecutor(out DefaultHttpContext httpContext);

        Task<IResult> ExecuteAsync() => executor.ExecuteAsync(
            httpContext,
            CreateOperation(),
            requireTenant: false,
            _ => Task.FromResult(Result.Success("value")),
            CancellationToken.None,
            onSuccess: _ => null!);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(ExecuteAsync);
        Assert.Contains("success result callback", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Non_generic_execute_rejects_null_action()
    {
        AdminApiExecutor executor = CreateExecutor(out DefaultHttpContext httpContext);
        Func<CancellationToken, Task<Result>> action = null!;

        Task<IResult> ExecuteAsync() =>
            executor.ExecuteAsync(
                httpContext,
                CreateOperation(),
                requireTenant: false,
                action,
                CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentNullException>(ExecuteAsync);
    }

    [Fact]
    public async Task Audit_failure_is_exposed_through_the_response_header()
    {
        AdminApiExecutor executor = CreateExecutor(out DefaultHttpContext httpContext, auditError: "Admin audit failed.");

        await executor.ExecuteAsync(
            httpContext,
            CreateOperation(),
            requireTenant: false,
            _ => Task.FromResult(Result.Success("value")),
            CancellationToken.None);

        Assert.Equal("failed", httpContext.Response.Headers["X-Admin-Audit"]);
    }

    [Fact]
    public async Task Configured_authentication_assurance_is_audited_and_challenged()
    {
        AuthenticationAssuranceRequirement requirement = new(
            ["urn:test:acr:mfa"],
            TimeSpan.FromMinutes(10));
        RecordingAdminOperationRunner runner = new();
        AdminApiExecutor executor = CreateExecutor(
            out DefaultHttpContext httpContext,
            runner: runner,
            assurance: requirement);
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "actor"),
                new Claim(ApplicationClaimNames.AuthenticationContextReference, "urn:test:acr:password")
            ],
            authenticationType: "Test"));

        IResult result = await executor.ExecuteAsync(
            httpContext,
            CreateOperation(),
            requireTenant: false,
            _ => Task.FromResult(Result.Success("value")),
            CancellationToken.None);

        await result.ExecuteAsync(httpContext);

        Assert.NotNull(runner.Context);
        Assert.Equal(
            AuthenticationAssuranceHttpErrorCodes.InsufficientAuthentication,
            runner.Context.PreAuthorizationError?.Code);
        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
        Assert.Contains(
            "insufficient_user_authentication",
            httpContext.Response.Headers.WWWAuthenticate.ToString());
    }

    [Fact]
    public async Task Resource_scope_resolver_is_forwarded_to_the_operation_runner()
    {
        AdminResourceScope resourceScope = AdminResourceScope.Create(
            AdminResourceScopeSegment.Create("property", "property-a"));
        RecordingAdminOperationRunner runner = new();
        AdminApiExecutor executor = CreateExecutor(
            out DefaultHttpContext httpContext,
            runner: runner,
            resourceScopeResolver: new FixedResourceScopeResolver(resourceScope));

        await executor.ExecuteAsync(
            httpContext,
            CreateOperation(),
            requireTenant: false,
            _ => Task.FromResult(Result.Success("value")),
            CancellationToken.None);

        Assert.Same(resourceScope, runner.Context?.ResourceScope);
    }

    [Fact]
    public async Task Invalid_resource_scope_is_audited_as_pre_authorization_failure()
    {
        RecordingAdminOperationRunner runner = new();
        AdminApiExecutor executor = CreateExecutor(
            out DefaultHttpContext httpContext,
            runner: runner,
            resourceScopeResolver: new FixedResourceScopeResolver(resourceScope: null, isValid: false));

        await executor.ExecuteAsync(
            httpContext,
            CreateOperation(),
            requireTenant: false,
            _ => Task.FromResult(Result.Success("value")),
            CancellationToken.None);

        Assert.Equal(
            AdminErrors.ResourceScopeInvalid,
            runner.Context?.PreAuthorizationError);
    }

    private static AdminApiExecutor CreateExecutor(
        out DefaultHttpContext httpContext,
        string? auditError = null,
        IAdminOperationRunner? runner = null,
        AuthenticationAssuranceRequirement? assurance = null,
        IAdminApiResourceScopeResolver? resourceScopeResolver = null)
    {
        ServiceCollection serviceCollection = new();
        serviceCollection.AddLogging();
        serviceCollection.AddSingleton(runner ?? new InvokingAdminOperationRunner(auditError));
        ServiceProvider services = serviceCollection.BuildServiceProvider();

        httpContext = new DefaultHttpContext
        {
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "actor")],
                authenticationType: "Test"))
        };

        return new AdminApiExecutor(
            Options.Create(new AdminApiOptions { AuthenticationAssurance = assurance }),
            Options.Create(new TenantOptions { Enabled = false }),
            resourceScopeResolver);
    }

    private static AdminOperation CreateOperation() =>
        AdminOperation.Create("admin.test", AdminPermission.Create("admin.test"));

    private sealed class InvokingAdminOperationRunner(string? auditError) : IAdminOperationRunner
    {
        public async Task<AdminOperationExecutionResult<T>> ExecuteAsync<T>(
            AdminOperationContext context,
            Func<CancellationToken, Task<Result<T>>> action,
            CancellationToken cancellationToken)
        {
            Result<T> result = await action(cancellationToken);
            return new AdminOperationExecutionResult<T>(
                result.IsSuccess ? AdminOperationExecutionStatus.Succeeded : AdminOperationExecutionStatus.Failed,
                result,
                auditError);
        }
    }

    private sealed class RecordingAdminOperationRunner : IAdminOperationRunner
    {
        public AdminOperationContext? Context { get; private set; }

        public Task<AdminOperationExecutionResult<T>> ExecuteAsync<T>(
            AdminOperationContext context,
            Func<CancellationToken, Task<Result<T>>> action,
            CancellationToken cancellationToken)
        {
            this.Context = context;
            Result<T> result = context.PreAuthorizationError is null
                ? Result.Failure<T>(AdminErrors.OperationFailed)
                : Result.Failure<T>(context.PreAuthorizationError);
            return Task.FromResult(new AdminOperationExecutionResult<T>(
                AdminOperationExecutionStatus.ValidationFailed,
                result,
                null));
        }
    }

    private sealed class FixedResourceScopeResolver(
        AdminResourceScope? resourceScope,
        bool isValid = true) : IAdminApiResourceScopeResolver
    {
        public bool TryResolve(
            HttpContext httpContext,
            out AdminResourceScope? resolvedResourceScope)
        {
            resolvedResourceScope = resourceScope;
            return isValid;
        }
    }
}
