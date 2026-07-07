namespace Gma.Framework.Tests;

using Microsoft.Extensions.DependencyInjection;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Cli;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using System.CommandLine;
using System.CommandLine.Parsing;
using Xunit;

[Trait("Category", "Unit")]
[Collection(ConsoleTestIsolation.Name)]
public sealed class AdminCliExecutorTests
{
    [Fact]
    public async Task Invalid_actor_returns_validation_failure_without_running_operation()
    {
        ServiceProvider services = new ServiceCollection()
            .AddGmaAdministrationCli()
            .BuildServiceProvider();
        AdminCliGlobalOptions options = services.GetRequiredService<AdminCliGlobalOptions>();
        RootCommand root = CreateRoot(options);
        ParseResult parseResult = root.Parse(["--actor", "actor 1"]);
        AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
        int actionExecutions = 0;

        using StringWriter error = new();
        TextWriter originalError = Console.Error;
        Console.SetError(error);

        try
        {
            int exitCode = await executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create("admin.test", AdminPermission.Create("admin.test")),
                tenantId: null,
                requireTenant: false,
                (_, _) =>
                {
                    actionExecutions++;
                    return Task.FromResult(Result.Success(Unit.Value));
                },
                CancellationToken.None);

            Assert.Equal(AdminExitCodes.ValidationFailed, exitCode);
        }
        finally
        {
            Console.SetError(originalError);
        }

        Assert.Equal(0, actionExecutions);
        Assert.Contains(AdminActor.InvalidIdMessage, error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_output_returns_validation_failure_without_running_operation()
    {
        ServiceProvider services = new ServiceCollection()
            .AddGmaAdministrationCli()
            .BuildServiceProvider();
        AdminCliGlobalOptions options = services.GetRequiredService<AdminCliGlobalOptions>();
        RootCommand root = CreateRoot(options);
        ParseResult parseResult = root.Parse(["--actor", "actor", "--output", "xml"]);
        AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
        int actionExecutions = 0;

        using StringWriter error = new();
        TextWriter originalError = Console.Error;
        Console.SetError(error);

        try
        {
            int exitCode = await executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create("admin.test", AdminPermission.Create("admin.test")),
                tenantId: null,
                requireTenant: false,
                (_, _) =>
                {
                    actionExecutions++;
                    return Task.FromResult(Result.Success(Unit.Value));
                },
                CancellationToken.None);

            Assert.Equal(AdminExitCodes.ValidationFailed, exitCode);
        }
        finally
        {
            Console.SetError(originalError);
        }

        Assert.Equal(0, actionExecutions);
        Assert.Contains(AdminCliOutput.InvalidOutputMessage, error.ToString(), StringComparison.Ordinal);
    }

    private static RootCommand CreateRoot(AdminCliGlobalOptions options)
    {
        RootCommand root = new("admin");
        root.Options.Add(options.ActorOption);
        root.Options.Add(options.OutputOption);
        return root;
    }
}
