namespace Gma.Framework.Administration.Cli;

using Microsoft.Extensions.DependencyInjection;
using Gma.Framework.Administration;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using System.CommandLine;
using System.CommandLine.Parsing;

public sealed class AdminCliExecutor(IServiceProvider serviceProvider)
{
    public async Task<int> ExecuteAsync<T>(
        ParseResult parseResult,
        AdminOperation operation,
        string? tenantId,
        bool requireTenant,
        Func<IServiceProvider, CancellationToken, Task<Result<T>>> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parseResult);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(action);

        using IServiceScope scope = serviceProvider.CreateScope();
        IServiceProvider scopedProvider = scope.ServiceProvider;
        AdminCliGlobalOptions options = scopedProvider.GetRequiredService<AdminCliGlobalOptions>();
        if (!AdminCliOutput.TryNormalizeFormat(parseResult.GetValue(options.OutputOption), out _))
        {
            return WriteErrorAndReturn(AdminCliOutput.InvalidOutputMessage, AdminExitCodes.ValidationFailed);
        }

        if (!AdminActor.TrySystem(ResolveActor(parseResult, options), out AdminActor? actor))
        {
            return WriteErrorAndReturn(
                AdminActor.InvalidIdMessage,
                AdminExitCodes.ValidationFailed);
        }

        IAdminOperationRunner runner = scopedProvider.GetRequiredService<IAdminOperationRunner>();
        AdminOperationExecutionResult<T> execution = await runner.ExecuteAsync(
            new AdminOperationContext(actor, operation, tenantId, requireTenant),
            token => action(scopedProvider, token),
            cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(execution.AuditError))
        {
            WriteError(execution.AuditError);
        }

        return execution.Status switch
        {
            AdminOperationExecutionStatus.Succeeded => AdminExitCodes.Success,
            AdminOperationExecutionStatus.Unauthorized => WriteErrorAndReturn(execution.Result.Error.Message, AdminExitCodes.Unauthorized),
            AdminOperationExecutionStatus.ValidationFailed => WriteErrorAndReturn(execution.Result.Error.Message, AdminExitCodes.ValidationFailed),
            _ => WriteErrorAndReturn(execution.Result.Error.Message, AdminExitCodes.Failed)
        };
    }

    public Task<int> ExecuteAsync(
        ParseResult parseResult,
        AdminOperation operation,
        string? tenantId,
        bool requireTenant,
        Func<IServiceProvider, CancellationToken, Task<Result>> action,
        CancellationToken cancellationToken) =>
        this.ExecuteAsync(
            parseResult,
            operation,
            tenantId,
            requireTenant,
            async (provider, token) =>
            {
                Result result = await action(provider, token).ConfigureAwait(false);
                return result.IsSuccess
                    ? Result.Success(Unit.Value)
                    : Result.Failure<Unit>(result.Error);
            },
            cancellationToken);

    private static string ResolveActor(ParseResult parseResult, AdminCliGlobalOptions options)
    {
        string? actor = parseResult.GetValue(options.ActorOption);

        if (!string.IsNullOrWhiteSpace(actor))
        {
            return actor;
        }

        return $"{Environment.UserDomainName}\\{Environment.UserName}";
    }

    private static int WriteErrorAndReturn(string message, int exitCode)
    {
        WriteError(message);
        return exitCode;
    }

    private static void WriteError(string message) => AdminCliOutput.WriteError(message);
}
