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

    private static AdminApiExecutor CreateExecutor(out DefaultHttpContext httpContext)
    {
        ServiceProvider services = new ServiceCollection()
            .AddSingleton<IAdminOperationRunner, InvokingAdminOperationRunner>()
            .BuildServiceProvider();

        httpContext = new DefaultHttpContext
        {
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "actor")],
                authenticationType: "Test"))
        };

        return new AdminApiExecutor(
            Options.Create(new AdminApiOptions()),
            Options.Create(new TenantOptions { Enabled = false }));
    }

    private static AdminOperation CreateOperation() =>
        AdminOperation.Create("admin.test", AdminPermission.Create("admin.test"));

    private sealed class InvokingAdminOperationRunner : IAdminOperationRunner
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
                auditError: null);
        }
    }
}
